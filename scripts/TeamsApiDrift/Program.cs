namespace Microsoft.Agents.TeamsApiDrift;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 2;
            }

            var options = new Arguments(args.Skip(1));
            return args[0] switch
            {
                "resolve-version" => ResolveVersion(options),
                "compare" => await CompareAsync(options).ConfigureAwait(false),
                "collect-usage" => CollectUsage(options),
                "validate-usage" => ValidateUsage(options),
                "validate-metadata" => ValidateMetadata(options),
                "classify" => Classify(options),
                "write-test-summary" => WriteTestSummary(options),
                "render-report" => RenderReport(options),
                "prepare-agent-context" => PrepareAgentContext(options),
                "validate-agent-report" => ValidateAgentReport(options),
                "prepare-usage-assessment" => PrepareUsageAssessment(options),
                "validate-usage-assessment" => ValidateUsageAssessment(options),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"TeamsApiDrift: {exception.Message}");
            return 2;
        }
    }

    private static int ResolveVersion(Arguments options)
    {
        using TextReader reader = options.Optional("--props") is "-" or null
            ? Console.In
            : File.OpenText(options.Required("--props"));
        var package = options.Optional("--package") ?? PackageConstants.PackageId;
        var version = VersionResolver.Resolve(reader, package);
        StepLog.Write("Resolved package version", $"{package}: {version}", $"Source: {options.Optional("--props") ?? "standard input"}");
        Console.WriteLine(version);
        return 0;
    }

    private static async Task<int> CompareAsync(Arguments options)
    {
        var service = new PackageApiService(options.Many("--source"), options.Optional("--config-file"));
        var from = options.Required("--from");
        var to = options.Optional("--to") ?? await service.GetLatestStableVersionAsync().ConfigureAwait(false);
        var output = Path.GetFullPath(options.Optional("--output") ?? PackageConstants.ArtifactDirectory);
        var before = await service.ExtractAsync(from).ConfigureAwait(false);
        var after = await service.ExtractAsync(to).ConfigureAwait(false);
        var comparison = ApiComparer.Compare(before, after);
        ToolJson.Write(Path.Combine(output, "microsoft-teams-apps-before.api.json"), before);
        ToolJson.Write(Path.Combine(output, "microsoft-teams-apps-after.api.json"), after);
        ToolJson.Write(Path.Combine(output, "raw-api-diff.json"), comparison);
        var affectedFrameworks = string.Join(", ", comparison.Changes.SelectMany(change => change.TargetFrameworks).Distinct().Order(StringComparer.Ordinal));
        StepLog.Write("Compared package metadata", $"{PackageConstants.PackageId}: {from} -> {to}",
            $"API changes: {comparison.Changes.Count}; affected frameworks: {(affectedFrameworks.Length == 0 ? "none" : affectedFrameworks)}",
            $"Before/after API models and raw diff written to: {output}");
        StepLog.Counts("API change types", comparison.Changes.Select(change => change.Kind));
        Console.WriteLine(to);
        return 0;
    }

    private static int CollectUsage(Arguments options)
    {
        var assemblies = options.Many("--assembly");
        if (assemblies.Count == 0) throw new ArgumentException("At least one --assembly is required.");
        var result = CollectUsage(assemblies);
        var output = ToolJson.OutputFile(options.Required("--output"), "collected-usage.json", ".json");
        ToolJson.Write(output, result);
        StepLog.Write("Collected Teams API usage", $"Assemblies inspected: {assemblies.Count}",
            $"Upstream symbols: {result.Usages.Count}; referenced members: {result.Usages.Sum(usage => usage.Members.Count)}; source files: {result.SourceFiles?.Count ?? 0}",
            $"Usage artifact: {output}");
        StepLog.Counts("Exposure", result.Usages.Select(usage => usage.Exposure));
        return 0;
    }

    private static CollectedUsage CollectUsage(IEnumerable<string> assemblies)
    {
        var collected = assemblies.Select(AssemblyUsageCollector.Collect).ToArray();
        var usages = collected.SelectMany(item => item.Usages)
            .GroupBy(item => item.UpstreamSymbol, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CollectedUsageEntry(
                group.Key,
                group.SelectMany(item => item.Members).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                group.Any(item => item.Exposure == "publicly-exposed") ? "publicly-exposed" : "internal-only"))
            .ToArray();
        var sourceFiles = collected.SelectMany(item => item.SourceFiles ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return new CollectedUsage(1, PackageConstants.PackageId, string.Join(",", collected.Select(item => item.Assembly)), usages, sourceFiles);
    }

    private static int ValidateUsage(Arguments options)
    {
        var result = UsageValidator.Validate(
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CollectedUsage>(options.Required("--collected")),
            ToolJson.Read<ApiModel>(options.Required("--api-model")),
            options.Optional("--repository-root") ?? Directory.GetCurrentDirectory());
        var output = ToolJson.OutputFile(options.Required("--output"), "usage-validation.json", ".json");
        ToolJson.Write(output, result);
        StepLog.Write($"Usage manifest validation {(result.Valid ? "passed" : "FAILED")}",
            $"Errors: {result.Errors.Count}; warnings: {result.Warnings.Count}; missing symbols: {result.MissingSymbols.Count}; missing members: {result.MissingMembers.Count}",
            $"Validation artifact: {output}");
        foreach (var error in result.Errors) Console.Error.WriteLine($"  Error: {error}");
        foreach (var warning in result.Warnings) Console.Error.WriteLine($"  Warning: {warning}");
        return result.Valid ? 0 : 1;
    }

    private static int ValidateMetadata(Arguments options)
    {
        var assemblies = options.Many("--assembly");
        if (assemblies.Count == 0) throw new ArgumentException("At least one --assembly is required.");
        var propsPath = options.Required("--props");
        using var props = File.OpenText(propsPath);
        var result = MetadataValidator.Validate(
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CapabilityDocument>(options.Required("--capabilities")),
            CollectUsage(assemblies),
            VersionResolver.Resolve(props),
            options.Optional("--repository-root") ?? Directory.GetCurrentDirectory(),
            options.Required("--manifest"),
            options.Required("--capabilities"),
            options.Optional("--base-ref"));
        StepLog.Write($"Repository metadata validation {(result.Valid ? "passed" : "FAILED")}", $"Findings: {result.Findings.Count}");
        foreach (var finding in result.Findings)
        {
            Console.Error.WriteLine($"TeamsApiDrift: [{finding.RuleId}] {finding.Path}: {finding.Message} Fix: {finding.Fix}");
        }
        return result.Valid ? 0 : 1;
    }

    private static int Classify(Arguments options)
    {
        var result = FindingClassifier.Classify(
            ToolJson.Read<ApiComparison>(options.Required("--comparison")),
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CapabilityDocument>(options.Required("--capabilities")));
        var output = ToolJson.OutputFile(options.Required("--output"), "findings.json", ".json");
        ToolJson.Write(output, result);
        StepLog.Findings(result, "Classified direct compatibility impact (before AI assessment)", output);
        return options.HasFlag("--fail-on-drift") && result.Summary.Blocking + result.Summary.Required > 0 ? 1 : 0;
    }

    private static int WriteTestSummary(Arguments options)
    {
        var checks = options.Many("--check").Select(value => value.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts.Length == 2 ? parts[1] : "unknown", StringComparer.Ordinal);
        var output = ToolJson.OutputFile(options.Required("--output"), "test-summary.json", ".json");
        ToolJson.Write(output, new TestSummary(1, checks));
        StepLog.Write("Recorded build and test outcomes", $"Checks recorded: {checks.Count}", $"Check artifact: {output}");
        StepLog.Counts("Outcomes", checks.Values);
        foreach (var check in checks.OrderBy(check => check.Key, StringComparer.Ordinal)) Console.Error.WriteLine($"  {check.Key}: {check.Value}");
        return 0;
    }

    private static int RenderReport(Arguments options)
    {
        var summaryPath = options.Optional("--test-summary");
        var markdown = DeterministicReportRenderer.Render(
            ToolJson.Read<FindingsResult>(options.Required("--findings")),
            summaryPath is null ? null : ToolJson.Read<TestSummary>(summaryPath));
        var output = ToolJson.OutputFile(options.Required("--output"), "deterministic-report.md", ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, markdown);
        StepLog.Write("Rendered deterministic impact report", $"Report: {output}", $"Lines: {markdown.Split('\n').Length}");
        return 0;
    }

    private static int PrepareAgentContext(Arguments options)
    {
        var summaryPath = options.Optional("--test-summary");
        var context = AgentContextBuilder.Build(
            ToolJson.Read<FindingsResult>(options.Required("--findings")),
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CapabilityDocument>(options.Required("--capabilities")),
            File.ReadAllText(options.Required("--deterministic-report")),
            summaryPath is null ? null : ToolJson.Read<TestSummary>(summaryPath),
            options.Optional("--repository-root") ?? Directory.GetCurrentDirectory());
        var output = ToolJson.OutputFile(options.Required("--output"), "agent-context.json", ".json");
        ToolJson.Write(output, context);
        var document = System.Text.Json.JsonSerializer.SerializeToElement(context, ToolJson.Options);
        var files = document.GetProperty("relevantSourceFiles");
        StepLog.Write("Prepared bounded advisory context", $"Source files: {files.GetArrayLength()}; truncated: {files.EnumerateArray().Count(file => file.GetProperty("truncated").GetBoolean())}; omitted: {document.GetProperty("omittedSourceFiles").GetArrayLength()}",
            $"Context artifact: {output}");
        return 0;
    }

    private static int ValidateAgentReport(Arguments options)
    {
        var validation = AgentReportValidator.Validate(
            File.ReadAllText(options.Required("--report")),
            ToolJson.Read<FindingsResult>(options.Required("--findings")));
        var output = ToolJson.OutputFile(options.Required("--output"), "agent-report-validation.json", ".json");
        ToolJson.Write(output, validation);
        StepLog.Write($"Advisory report validation {(validation.Valid ? "passed" : "FAILED")}",
            $"Referenced findings: {validation.ReferencedFindingIds.Count}; missing mandatory findings: {validation.MissingMandatoryFindingIds.Count}; unknown findings: {validation.UnknownFindingIds.Count}; errors: {validation.Errors.Count}",
            $"Validation artifact: {output}");
        if (!validation.Valid)
        {
            Console.Error.WriteLine($"TeamsApiDrift: Agent report validation failed. Details were written to '{output}'.");
            foreach (var error in validation.Errors)
            {
                Console.Error.WriteLine($"TeamsApiDrift: {error}");
            }
        }
        return validation.Valid ? 0 : 1;
    }

    private static int PrepareUsageAssessment(Arguments options)
    {
        var context = UsageAssessmentPipeline.Prepare(
            ToolJson.Read<FindingsResult>(options.Required("--findings")),
            ToolJson.Read<TestSummary>(options.Required("--test-summary")),
            options.Optional("--repository-root") ?? Directory.GetCurrentDirectory());
        ToolJson.Write(options.Required("--output"), context);
        StepLog.Write("Prepared nullability usage assessment", $"Eligible findings: {context.Findings.Count}",
            $"Source/test files: {context.SourceFiles.Count}; truncated: {context.SourceFiles.Count(file => file.Truncated)}; omitted: {context.OmittedFiles.Count}",
            context.Findings.Count == 0 ? "AI usage assessment will be skipped: no eligible internal nullability changes." : "AI will assess these findings as safe or needs-human-review.",
            $"Context artifact: {options.Required("--output")}");
        Console.WriteLine(context.Findings.Count);
        return 0;
    }

    private static int ValidateUsageAssessment(Arguments options)
    {
        var gateOutput = options.Required("--gate-output");
        File.Delete(gateOutput);
        UsageAssessmentValidation validation;
        try
        {
            var findings = ToolJson.Read<FindingsResult>(options.Required("--findings"));
            var context = ToolJson.Read<UsageAssessmentContext>(options.Required("--context"));
            var jsonOptions = new System.Text.Json.JsonSerializerOptions(ToolJson.Options)
            {
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
            };
            var report = System.Text.Json.JsonSerializer.Deserialize<UsageAssessmentReport>(
                File.ReadAllText(options.Required("--report")), jsonOptions)
                ?? throw new InvalidDataException("Assessment report is empty.");
            validation = UsageAssessmentPipeline.Validate(report, context, findings);
            if (validation.Valid)
            {
                // Write the effective gate findings only after the complete report validates.
                var effective = UsageAssessmentPipeline.Apply(report, context, findings);
                var summary = UsageAssessmentPipeline.Render(report);
                var summaryPath = options.Required("--summary-output");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(summaryPath))!);
                File.WriteAllText(summaryPath, summary);
                ToolJson.Write(gateOutput, effective);
                StepLog.Write("Validated and applied AI usage assessments", $"Assessments: {report.Assessments.Count}",
                    $"Findings cleared by AI: {report.Assessments.Count(assessment => assessment.Verdict == "safe")}",
                    $"Findings needing human review: {report.Assessments.Count(assessment => assessment.Verdict == "needs-human-review")}");
                StepLog.Findings(effective, "Effective findings after AI assessment", gateOutput);
            }
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or IOException or ArgumentException)
        {
            validation = new(1, false, [exception.Message]);
        }
        ToolJson.Write(options.Required("--output"), validation);
        if (!validation.Valid) StepLog.Write("AI usage assessment validation FAILED", $"Errors: {validation.Errors.Count}", "No findings were cleared; the original policy findings remain in effect.");
        Console.Error.WriteLine($"  Assessment validation artifact: {options.Required("--output")}");
        foreach (var error in validation.Errors) Console.Error.WriteLine($"TeamsApiDrift: {error}");
        return validation.Valid ? 0 : 1;
    }

    private static void PrintUsage() => Console.Error.WriteLine(
        "Commands: resolve-version, compare, collect-usage, validate-usage, validate-metadata, classify, write-test-summary, render-report, prepare-agent-context, validate-agent-report, prepare-usage-assessment, validate-usage-assessment");
}

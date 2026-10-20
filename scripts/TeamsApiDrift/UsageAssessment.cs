using System.Text.Json;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record AssessmentSource(string Path, string Content, bool Truncated);
public sealed record UsageAssessmentContext(int SchemaVersion, string FromVersion, string ToVersion,
    IReadOnlyList<Finding> Findings, TestSummary TestSummary, IReadOnlyList<AssessmentSource> SourceFiles, IReadOnlyList<string> OmittedFiles);
public sealed record UsageEvidence(string Path, int StartLine, int EndLine, string Quote);
public sealed record UsageAssessment(string FindingId, string Verdict, string Reason, IReadOnlyList<UsageEvidence> Evidence);
public sealed record UsageAssessmentReport(int SchemaVersion, string FromVersion, string ToVersion, IReadOnlyList<UsageAssessment> Assessments);
public sealed record UsageAssessmentValidation(int SchemaVersion, bool Valid, IReadOnlyList<string> Errors);

public static class UsageAssessmentPipeline
{
    private const string TestRoot = "src/tests/Microsoft.Agents.Extensions.MSTeams.Tests";
    private const int MaximumFileLength = 12_000;
    private const int MaximumTotalLength = 48_000;

    public static bool IsEligible(Finding finding) => finding.Kind == "nullability-changed" &&
        finding.Exposure == "internal-only" && finding.Category == "nullability-review" &&
        finding.Classification is "required" or "review";

    public static UsageAssessmentContext Prepare(FindingsResult findings, TestSummary summary, string repositoryRoot)
    {
        var candidates = findings.Findings.Where(IsEligible).ToArray();
        var affected = candidates.SelectMany(finding => finding.AffectedFiles).Select(Paths.Normalize).Distinct(StringComparer.Ordinal).ToArray();
        var paths = new SortedSet<string>(affected, StringComparer.Ordinal);
        var omitted = new List<string>();
        var sources = new List<AssessmentSource>();
        if (candidates.Length > 0)
        {
            // Include other accesses/callers and relevant tests, in addition to the manifest's files.
            var terms = candidates.SelectMany(finding => new[] { finding.Member, finding.UpstreamSymbol.Split('.').Last() })
                .Where(term => !string.IsNullOrWhiteSpace(term)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
            foreach (var relativeRoot in new[] { PackageConstants.SourceRoot, TestRoot })
            {
                var root = Path.GetFullPath(Path.Combine(repositoryRoot, relativeRoot));
                AgentContextBuilder.RejectSymlinkTraversal(root, root, relativeRoot);
                if (!Directory.Exists(root)) { omitted.Add(relativeRoot); continue; }
                foreach (var file in EnumerateSources(root))
                {
                    var relative = Paths.Normalize(Path.GetRelativePath(repositoryRoot, file));
                    AgentContextBuilder.RejectSymlinkTraversal(root, file, relative);
                    var text = File.ReadAllText(file);
                    if (terms.Any(term => text.Contains(term, StringComparison.Ordinal)) ||
                        affected.Any(path => Path.GetFileNameWithoutExtension(file).StartsWith(Path.GetFileNameWithoutExtension(path), StringComparison.Ordinal)))
                        paths.Add(relative);
                }
            }
        }

        var remaining = MaximumTotalLength;
        foreach (var path in paths.OrderBy(path => affected.Contains(path, StringComparer.Ordinal) ? 0 : 1).ThenBy(path => path, StringComparer.Ordinal))
        {
            var full = Path.GetFullPath(Path.Combine(repositoryRoot, path));
            var allowed = new[] { PackageConstants.SourceRoot, TestRoot }.Select(root => Path.GetFullPath(Path.Combine(repositoryRoot, root)))
                .FirstOrDefault(root => Paths.IsContainedBy(root, full));
            if (allowed is null) throw new InvalidDataException($"Assessment source path escapes the allowed roots: {path}");
            AgentContextBuilder.RejectSymlinkTraversal(allowed, full, path);
            if (!File.Exists(full) || Path.GetExtension(full) != ".cs" || remaining == 0)
            {
                omitted.Add(path);
                continue;
            }
            var content = AgentContextBuilder.Redact(File.ReadAllText(full)).ReplaceLineEndings("\n");
            var length = Math.Min(content.Length, Math.Min(MaximumFileLength, remaining));
            sources.Add(new(path, content[..length], length < content.Length));
            remaining -= length;
        }
        return new(1, findings.FromVersion, findings.ToVersion, candidates, summary, sources, omitted);
    }

    private static IEnumerable<string> EnumerateSources(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs").Order(StringComparer.Ordinal)) yield return file;
        foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (Path.GetFileName(child) is "bin" or "obj") continue;
            AgentContextBuilder.RejectSymlinkTraversal(directory, child, child);
            foreach (var file in EnumerateSources(child)) yield return file;
        }
    }

    public static UsageAssessmentValidation Validate(UsageAssessmentReport report, UsageAssessmentContext context, FindingsResult findings)
    {
        var errors = new List<string>();
        if (report.SchemaVersion != 1 || context.SchemaVersion != 1) errors.Add("Unsupported assessment schema version.");
        if (report.FromVersion != findings.FromVersion || report.ToVersion != findings.ToVersion ||
            context.FromVersion != findings.FromVersion || context.ToVersion != findings.ToVersion)
            errors.Add("Assessment versions do not match the current comparison.");
        var expected = findings.Findings.Where(IsEligible).ToArray();
        if (JsonSerializer.Serialize(expected, ToolJson.Options) != JsonSerializer.Serialize(context.Findings, ToolJson.Options))
            errors.Add("Assessment context does not match the current eligible findings.");
        if (report.Assessments is null) return new(1, false, ["Assessment report must contain an assessments array."]);
        var assessments = report.Assessments;
        if (assessments.Any(item => item is null)) return new(1, false, ["Assessment entries must be objects."]);
        if (assessments.GroupBy(item => item.FindingId).Any(group => group.Count() != 1)) errors.Add("Duplicate assessment IDs.");
        foreach (var finding in expected)
            if (!assessments.Any(item => item.FindingId == finding.Id)) errors.Add($"Missing assessment: {finding.Id}.");
        foreach (var assessment in assessments)
        {
            var finding = expected.FirstOrDefault(item => item.Id == assessment.FindingId);
            if (finding is null) { errors.Add($"Unknown or ineligible assessment: {assessment.FindingId}."); continue; }
            if (assessment.Verdict is not ("safe" or "needs-human-review")) errors.Add($"Invalid verdict: {finding.Id}.");
            if (string.IsNullOrWhiteSpace(assessment.Reason)) errors.Add($"Missing reasoning: {finding.Id}.");
            var evidence = assessment.Evidence ?? [];
            if (evidence.Any(item => item is null)) { errors.Add($"Evidence entries must be objects: {finding.Id}."); continue; }
            foreach (var citation in evidence)
            {
                var source = context.SourceFiles.SingleOrDefault(file => file.Path == citation.Path);
                var lines = source?.Content.Split('\n') ?? [];
                if (citation.StartLine < 1 || citation.EndLine < citation.StartLine || citation.EndLine > lines.Length ||
                    string.IsNullOrWhiteSpace(citation.Quote) ||
                    string.Join("\n", lines.Skip(citation.StartLine - 1).Take(citation.EndLine - citation.StartLine + 1)) != citation.Quote.ReplaceLineEndings("\n"))
                    errors.Add($"Invalid source citation for {finding.Id}: {citation.Path}.");
            }
            if (assessment.Verdict != "safe") continue;
            if (context.OmittedFiles.Count > 0 || context.SourceFiles.Any(file => file.Truncated))
                errors.Add($"Safe verdict requires complete source context: {finding.Id}.");
            if (!ChecksPassed(context.TestSummary)) errors.Add($"Safe verdict requires passing build, manifest, contract and boundary checks: {finding.Id}.");
            if (finding.AffectedFiles.Count == 0 || finding.AffectedFiles.Any(path => !evidence.Any(item => item.Path == Paths.Normalize(path))))
                errors.Add($"Safe verdict must cite every affected source file: {finding.Id}.");
            if (!evidence.Any(item => item.Path?.StartsWith(TestRoot + "/", StringComparison.Ordinal) == true))
                errors.Add($"Safe verdict requires test source evidence: {finding.Id}.");
        }
        return new(1, errors.Count == 0, errors);
    }

    private static bool ChecksPassed(TestSummary summary)
    {
        var checks = summary.Checks;
        bool Passed(string name) => checks.TryGetValue(name, out var status) && status == "success";
        return Passed("build") && Passed("manifest") && Passed("compare") && Passed("drift-tests") &&
            (Passed("candidate-tests") || new[] { "contract-net8", "contract-net10", "boundary-net8", "boundary-net10" }.All(Passed));
    }

    public static FindingsResult Apply(UsageAssessmentReport report, UsageAssessmentContext context, FindingsResult findings)
    {
        var validation = Validate(report, context, findings);
        if (!validation.Valid) throw new InvalidDataException(string.Join(" ", validation.Errors));
        var safe = report.Assessments.Where(item => item.Verdict == "safe").Select(item => item.FindingId).ToHashSet(StringComparer.Ordinal);
        var updated = findings.Findings.Select(finding => safe.Contains(finding.Id)
            ? finding with { Classification = "no-action", Category = "ai-assessed-safe", Evidence = [.. finding.Evidence, "ai-usage-assessment"], RecommendedAction = "AI assessed this internal nullability change as safe; see usage-assessments.json for reasoning and source evidence." }
            : finding).ToArray();
        return findings with { Findings = updated, Summary = new(updated.Count(item => item.Classification == "blocking"), updated.Count(item => item.Classification == "required"), updated.Count(item => item.Classification == "review"), updated.Count(item => item.Classification == "no-action")) };
    }

    public static string Render(UsageAssessmentReport report) => string.Join("\n", new[]
    {
        "## AI nullability usage assessment", "",
        "Validated safe assessments clear only their internal nullability findings. Original findings remain in findings.json.", ""
    }.Concat(report.Assessments.SelectMany(item => new[]
    {
        $"- **{item.FindingId}**: **{item.Verdict}** — {item.Reason.ReplaceLineEndings(" ")}",
        $"  Evidence: {string.Join(", ", (item.Evidence ?? []).Select(citation => $"`{citation.Path}:{citation.StartLine}-{citation.EndLine}`"))}"
    }))) + "\n";
}

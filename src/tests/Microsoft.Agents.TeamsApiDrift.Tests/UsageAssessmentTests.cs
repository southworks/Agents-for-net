using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class UsageAssessmentTests
{
    private const string SourcePath = PackageConstants.SourceRoot + "/SelectedChannel.cs";
    private const string TestPath = "src/tests/Microsoft.Agents.Extensions.MSTeams.Tests/SelectedChannelTests.cs";
    private const string Source = "return channelData?.Settings?.SelectedChannel?.Id;";
    private const string TestSource = "Assert.Null(activity.TeamsGetSelectedChannelId());";

    [Fact]
    public void SafeAssessmentClearsOnlyEligibleFindingAndPreservesOriginal()
    {
        var original = Findings(Eligible(), Eligible() with { Id = "MTAPI-0002", Kind = "member-removed", Classification = "blocking", Category = null });
        var effective = UsageAssessmentPipeline.Apply(Report(), Context(), original);

        Assert.Equal(1, original.Summary.Required);
        Assert.Equal(0, effective.Summary.Required);
        Assert.Equal(1, effective.Summary.Blocking);
        Assert.Equal("ai-assessed-safe", effective.Findings[0].Category);
        Assert.Contains("ai-usage-assessment", effective.Findings[0].Evidence);
        Assert.Equal(original.Findings[1], effective.Findings[1]);
    }

    [Fact]
    public void HumanReviewRetainsPolicyFailureWithoutRequiringCompleteEvidence()
    {
        var report = Report() with { Assessments = [Assessment() with { Verdict = "needs-human-review", Evidence = [] }] };
        var context = Context() with { OmittedFiles = [SourcePath], SourceFiles = [], TestSummary = new(1, new Dictionary<string, string>()) };
        var result = UsageAssessmentPipeline.Apply(report, context, Findings(Eligible()));
        Assert.Equal(1, result.Summary.Required);
        Assert.Equal("required", Assert.Single(result.Findings).Classification);
    }

    [Theory]
    [InlineData("publicly-exposed", "nullability-changed", "nullability-review", "blocking")]
    [InlineData("internal-only", "member-signature-changed", "nullability-review", "required")]
    [InlineData("internal-only", "nullability-changed", "reviewed-compatible", "no-action")]
    public void IneligibleFindingsCannotBeCleared(string exposure, string kind, string category, string classification)
    {
        var finding = Eligible() with { Exposure = exposure, Kind = kind, Category = category, Classification = classification };
        var findings = Findings(finding);
        var context = Context() with { Findings = [] };
        Assert.False(UsageAssessmentPipeline.Validate(Report(), context, findings).Valid);
        Assert.Throws<InvalidDataException>(() => UsageAssessmentPipeline.Apply(Report(), context, findings));
    }

    [Fact]
    public void RejectsMissingDuplicateUnknownAndInvalidAssessments()
    {
        var invalid = new[]
        {
            Report() with { Assessments = [] },
            Report() with { Assessments = null! },
            Report() with { Assessments = [null!] },
            Report() with { Assessments = [Assessment() with { Evidence = [null!] }] },
            Report() with { Assessments = [Assessment() with { Evidence = [new(null!, 1, 1, Source)] }] },
            Report() with { Assessments = [Assessment(), Assessment()] },
            Report() with { Assessments = [Assessment() with { FindingId = "MTAPI-9999" }] },
            Report() with { Assessments = [Assessment() with { Verdict = "probably-safe" }] },
            Report() with { Assessments = [Assessment() with { Reason = " " }] },
            Report() with { SchemaVersion = 2 },
            Report() with { ToVersion = "3.0.0" }
        };
        Assert.All(invalid, report => Assert.False(UsageAssessmentPipeline.Validate(report, Context(), Findings(Eligible())).Valid));
    }

    [Fact]
    public void RejectsStaleFindingSignaturesEvenWhenIdMatches()
    {
        var findings = Findings(Eligible() with { After = "different signature" });
        Assert.False(UsageAssessmentPipeline.Validate(Report(), Context(), findings).Valid);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("manifest")]
    [InlineData("compare")]
    [InlineData("drift-tests")]
    [InlineData("contract-net8")]
    [InlineData("contract-net10")]
    [InlineData("boundary-net8")]
    [InlineData("boundary-net10")]
    public void SafeAssessmentCannotOverrideFailedChecks(string failedCheck)
    {
        var checks = PassingChecks();
        checks[failedCheck] = "failure";
        var context = Context() with { TestSummary = new(1, checks) };
        Assert.False(UsageAssessmentPipeline.Validate(Report(), context, Findings(Eligible())).Valid);
    }

    [Fact]
    public void SupportsScheduledCombinedCandidateChecks()
    {
        var checks = new Dictionary<string, string> { ["build"] = "success", ["manifest"] = "success", ["compare"] = "success", ["drift-tests"] = "success", ["candidate-tests"] = "success" };
        Assert.True(UsageAssessmentPipeline.Validate(Report(), Context() with { TestSummary = new(1, checks) }, Findings(Eligible())).Valid);
    }

    [Fact]
    public void RejectsIncompleteSourceAndMissingImplementationOrTestEvidence()
    {
        var contexts = new[]
        {
            Context() with { OmittedFiles = [SourcePath] },
            Context() with { SourceFiles = [new(SourcePath, Source, true), new(TestPath, TestSource, false)] }
        };
        Assert.All(contexts, context => Assert.False(UsageAssessmentPipeline.Validate(Report(), context, Findings(Eligible())).Valid));
        foreach (var evidence in new[] { Array.Empty<UsageEvidence>(), new[] { new UsageEvidence(SourcePath, 1, 1, Source) }, new[] { new UsageEvidence(TestPath, 1, 1, TestSource) } })
            Assert.False(UsageAssessmentPipeline.Validate(Report() with { Assessments = [Assessment() with { Evidence = evidence }] }, Context(), Findings(Eligible())).Valid);
    }

    [Theory]
    [InlineData(SourcePath, 1, 1, "fabricated guard")]
    [InlineData(SourcePath, 0, 1, Source)]
    [InlineData(SourcePath, 1, 99, Source)]
    [InlineData("outside.cs", 1, 1, Source)]
    public void RejectsFabricatedOrInvalidSourceCitations(string path, int start, int end, string quote)
    {
        var report = Report() with { Assessments = [Assessment() with { Evidence = [new(path, start, end, quote), new(TestPath, 1, 1, TestSource)] }] };
        Assert.False(UsageAssessmentPipeline.Validate(report, Context(), Findings(Eligible())).Valid);
    }

    [Fact]
    public void PreparationCollectsRelatedSourceAndTestsAndMarksTruncation()
    {
        InRepository(root =>
        {
            Write(root, SourcePath, Source);
            Write(root, TestPath, TestSource);
            Write(root, PackageConstants.SourceRoot + "/Caller.cs", "Use(SelectedChannel);");
            var context = UsageAssessmentPipeline.Prepare(Findings(Eligible()), new(1, PassingChecks()), root);
            Assert.Equal(3, context.SourceFiles.Count);
            Assert.Empty(context.OmittedFiles);
            Write(root, TestPath, TestSource + new string('x', 13_000));
            context = UsageAssessmentPipeline.Prepare(Findings(Eligible()), new(1, PassingChecks()), root);
            Assert.True(context.SourceFiles.Single(file => file.Path == TestPath).Truncated);
        });
    }

    [Fact]
    public void PreparationRejectsEscapingManifestPaths()
    {
        InRepository(root => Assert.Throws<InvalidDataException>(() => UsageAssessmentPipeline.Prepare(
            Findings(Eligible() with { AffectedFiles = ["../outside.cs"] }), new(1, PassingChecks()), root)));
    }

    [Fact]
    public void PreparationRedactsSecretsAndRecordsMissingFiles()
    {
        InRepository(root =>
        {
            Write(root, SourcePath, Source + "\nvar token = \"secret-value\";");
            Write(root, TestPath, TestSource);
            var context = UsageAssessmentPipeline.Prepare(Findings(Eligible()), new(1, PassingChecks()), root);
            Assert.DoesNotContain("secret-value", context.SourceFiles.Single(file => file.Path == SourcePath).Content);
            var missing = Eligible() with { AffectedFiles = [PackageConstants.SourceRoot + "/Missing.cs"] };
            context = UsageAssessmentPipeline.Prepare(Findings(missing), new(1, PassingChecks()), root);
            Assert.Contains(missing.AffectedFiles[0], context.OmittedFiles);
        });
    }

    [Fact]
    public void ClassifierCreatesAssessmentCategoryWithoutWeakeningOriginalGate()
    {
        var change = new ApiChange("MTAPI-0001", "nullability-changed", "Contoso.Settings", "SelectedChannel", "before", "after", "potentially-breaking", ["net8.0"], []);
        var manifest = new UsageManifest { Usages = [new() { UpstreamSymbol = change.Symbol, Members = [change.Member!], Files = [SourcePath] }] };
        var result = FindingClassifier.Classify(new(1, PackageConstants.PackageId, "1.0.0", "2.0.0", true, [change]), manifest, new());
        Assert.Equal(1, result.Summary.Required);
        Assert.Equal("nullability-review", Assert.Single(result.Findings).Category);
    }

    private static Finding Eligible() => new("MTAPI-0001", "required", "nullability-review", "nullability-changed", "Contoso.Settings", "SelectedChannel", null, "internal-only", [SourcePath], "before", "after", ["net8.0", "net10.0"], [], "Review.");
    private static FindingsResult Findings(params Finding[] findings) => new(1, PackageConstants.PackageId, "1.0.0", "2.0.0", new(findings.Count(f => f.Classification == "blocking"), findings.Count(f => f.Classification == "required"), findings.Count(f => f.Classification == "review"), findings.Count(f => f.Classification == "no-action")), findings);
    private static Dictionary<string, string> PassingChecks() => new[] { "build", "manifest", "compare", "drift-tests", "contract-net8", "contract-net10", "boundary-net8", "boundary-net10" }.ToDictionary(check => check, _ => "success");
    private static UsageAssessmentContext Context() => new(1, "1.0.0", "2.0.0", [Eligible()], new(1, PassingChecks()), [new(SourcePath, Source, false), new(TestPath, TestSource, false)], []);
    private static UsageAssessment Assessment() => new("MTAPI-0001", "safe", "All accesses handle null; boundary tests cover absent and null values.", [new(SourcePath, 1, 1, Source), new(TestPath, 1, 1, TestSource)]);
    private static UsageAssessmentReport Report() => new(1, "1.0.0", "2.0.0", [Assessment()]);
    private static void Write(string root, string path, string content)
    {
        var full = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
    private static void InRepository(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), $"usage-assessment-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, recursive: true); }
    }
}

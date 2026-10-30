using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class ErgonomicsReviewTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ergonomics-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly ErgonomicsCatalog Catalog = new(1, [new("discoverability", "Discover known values.")]);
    private const string Props = "<Project><ItemGroup><PackageVersion Include=\"Microsoft.Teams.Apps\" Version=\"2.1.2\" /></ItemGroup></Project>";

    private ErgonomicsInput Prepare(string candidate = "2.3.0") => ErgonomicsReviewPipeline.Prepare(_root, Catalog,
        new[] { "agents", "teams-baseline", "teams-candidate" }.Select(role =>
            (new BehaviorSource(role, role == "teams-candidate" ? candidate : "2.1.2", "https://github.com/microsoft/teams.net", new string('a', 40)),
            new Dictionary<string, string>
            {
                ["src/Activity.cs"] = "public string Type { get; set; }\n// Known values\n",
                ["Directory.Packages.props"] = Props
            })).ToArray());

    private static ReviewCitation Cite(string role) => new(role + "/src/Activity.cs", StartLine: 1, EndLine: 2);
    private static ErgonomicsProposal Proposal() => new("Discover known activity types", "IActivity.Type", "MSTeams", "msteams",
        "Known values are hard to find.", "Document existing constants at the entry point.",
        "activity.Type = \"message\";", "activity.Type = ActivityTypes.Message;", "Discover known values through documentation.", "candidate",
        new("Existing signatures remain.", "Existing compiled consumers remain valid.", "Custom strings remain valid.",
            "The property still serializes as a string.", "Uses existing constants available with the declared dependency 2.1.2."),
        "Compile old/new examples and verify custom string round-trips.",
        [Cite("agents"), Cite("teams-baseline"), Cite("teams-candidate")]);

    private static ErgonomicsReview Review(ErgonomicsInput input, string outcome = "recommendations") =>
        new(1, input.ReviewId, [new("discoverability", outcome, "Inspected the entry point and known values.",
            outcome == "recommendations" ? [Proposal()] : [], [Cite("agents")], [Scenario()])]);

    private static ErgonomicsScenario Scenario() => new("Find a known activity type while composing a message.",
        "The caller assigns a string and needs to find the existing named constants.",
        "Link the existing constants from the caller's entry point rather than adding another value type.",
        "A documentation improvement can aid discovery while retaining custom strings.", [Cite("agents")]);

    [Theory]
    [InlineData("2.1.2")]
    [InlineData("2.3.0")]
    public void ReviewsDeclaredBaselineWithEqualOrDifferentCandidate(string candidate)
    {
        var input = Prepare(candidate);
        Assert.Equal("2.1.2", input.Sources.Single(s => s.Role == "teams-baseline").Version);
        Assert.Equal(candidate, input.Sources.Single(s => s.Role == "teams-candidate").Version);
        var result = ErgonomicsReviewPipeline.Validate(input, Review(input), _root);
        Assert.True(result.Valid, string.Join("\n", result.Errors));
        Assert.True(result.HasRecommendations);
        Assert.Equal("public string Type { get; set; }\n// Known values", result.Decisions[0].Proposals[0].Evidence[0].Quote);
        Assert.Equal("public string Type { get; set; }\n// Known values", result.Decisions[0].Scenarios[0].Evidence[0].Quote);
        var report = ErgonomicsReviewPipeline.Render(input, result);
        Assert.Contains("Discover known activity types", report);
        Assert.Contains("unimplemented", report);
        Assert.Contains("have not been compiled", report);
        Assert.Contains("Source compatibility", report);
        Assert.Contains("Works with the declared dependency", report);
        Assert.Contains($"https://github.com/microsoft/teams.net/blob/{new string('a', 40)}/src/Activity.cs#L1-L2", report);
    }

    [Fact]
    public void BaselineCannotBeReplacedWithDriftOverride()
    {
        Assert.Equal("2.1.2", ErgonomicsReviewPipeline.DeclaredVersion(new Dictionary<string, string> { ["Directory.Packages.props"] = Props }));
        Assert.Throws<InvalidDataException>(() => ErgonomicsReviewPipeline.Prepare(_root, Catalog,
            new[] { "agents", "teams-baseline", "teams-candidate" }.Select(role =>
                (new BehaviorSource(role, "9.0.0", "fixture", new string('a', 40)),
                    new Dictionary<string, string> { ["Directory.Packages.props"] = Props, ["src/File.cs"] = "source" })).ToArray()));
    }

    [Theory]
    [InlineData("no-improvement")]
    [InlineData("inconclusive")]
    public void EmptyAndInconclusiveReviewsAreValidAdvisoryResults(string outcome)
    {
        var input = Prepare();
        var result = ErgonomicsReviewPipeline.Validate(input, Review(input, outcome), _root);
        Assert.True(result.Valid);
        Assert.False(result.HasRecommendations);
        var report = ErgonomicsReviewPipeline.Render(input, result);
        Assert.Contains("No compatible API ergonomics improvements", report);
        Assert.Equal(outcome == "inconclusive", report.Contains("Review limitation"));
    }

    [Fact]
    public void RequiresCoverageAndConsistentOutcomes()
    {
        var input = Prepare();
        var review = Review(input);
        Assert.False(ErgonomicsReviewPipeline.Validate(input, review with { Decisions = [] }, _root).Valid);
        Assert.False(ErgonomicsReviewPipeline.Validate(input, review with { Decisions = [review.Decisions[0], review.Decisions[0]] }, _root).Valid);
        foreach (var decision in new[]
        {
            review.Decisions[0] with { CheckId = "unknown" },
            review.Decisions[0] with { Outcome = "safe" },
            review.Decisions[0] with { Outcome = "no-improvement" },
            review.Decisions[0] with { Proposals = [] }
        })
        {
            Assert.False(ErgonomicsReviewPipeline.Validate(input, review with { Decisions = [decision] }, _root).Valid);
        }
    }

    [Theory]
    [InlineData("recommendations")]
    [InlineData("no-improvement")]
    [InlineData("inconclusive")]
    public void EveryOutcomeRequiresCallerScenarios(string outcome)
    {
        var input = Prepare();
        var review = Review(input, outcome);
        var result = ErgonomicsReviewPipeline.Validate(input,
            review with { Decisions = [review.Decisions[0] with { Scenarios = [] }] }, _root);
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, error => error.Contains("concrete caller scenarios"));
    }

    [Fact]
    public void NoImprovementMustRecordTheAlternativeConsideredAndSupportingEvidence()
    {
        var input = Prepare();
        var review = Review(input, "no-improvement");
        var adequate = Scenario() with
        {
            CurrentExperience = "The caller finds the named constants through the documented entry point.",
            Assessment = "The alternative duplicates the existing documented path without reducing caller effort."
        };
        foreach (var scenario in new[]
        {
            adequate with { CallerTask = "" },
            adequate with { CurrentExperience = "" },
            adequate with { ImprovementConsidered = "" },
            adequate with { Assessment = "" },
            adequate with { Evidence = [] },
            adequate with { Evidence = [Cite("teams-baseline")] },
            adequate with { Evidence = [new("agents/src/Activity.cs", StartLine: 1, EndLine: 99)] }
        })
        {
            var result = ErgonomicsReviewPipeline.Validate(input,
                review with { Decisions = [review.Decisions[0] with { Scenarios = [scenario] }] }, _root);
            Assert.False(result.Valid);
        }
        Assert.True(ErgonomicsReviewPipeline.Validate(input,
            review with { Decisions = [review.Decisions[0] with { Scenarios = [adequate] }] }, _root).Valid);
    }

    [Fact]
    public void InconclusiveScenarioCanExplainUnavailableEvidence()
    {
        var input = Prepare();
        var review = Review(input, "inconclusive");
        var scenario = Scenario() with { Assessment = "Required implementation evidence is unavailable.", Evidence = [] };
        var result = ErgonomicsReviewPipeline.Validate(input,
            review with { Decisions = [review.Decisions[0] with { Scenarios = [scenario] }] }, _root);
        Assert.True(result.Valid);
        Assert.False(result.HasRecommendations);
    }

    [Fact]
    public void SingleLineEvidenceIsAllowedForSpecificDeclarationClaims()
    {
        var input = Prepare();
        var review = Review(input, "no-improvement");
        var scenario = Scenario() with
        {
            CallerTask = "Assign a custom activity type.",
            CurrentExperience = "The public Type property accepts a string.",
            ImprovementConsidered = "Keep the existing property instead of replacing its type.",
            Assessment = "The existing string signature supports this caller task.",
            Evidence = [new("agents/src/Activity.cs", StartLine: 1, EndLine: 1)]
        };
        var result = ErgonomicsReviewPipeline.Validate(input,
            review with { Decisions = [review.Decisions[0] with { Scenarios = [scenario] }] }, _root);
        Assert.True(result.Valid);
        Assert.Equal("public string Type { get; set; }", result.Decisions[0].Scenarios[0].Evidence[0].Quote);
    }

    [Fact]
    public void CandidateInspiredProposalsRequireBaselineEvidenceAndCompatibilityRationale()
    {
        var input = Prepare();
        var review = Review(input);
        foreach (var proposal in new[]
        {
            Proposal() with { Evidence = [Cite("agents"), Cite("teams-candidate")] },
            Proposal() with { Evidence = [Cite("teams-baseline"), Cite("teams-candidate")] },
            Proposal() with { Compatibility = Proposal().Compatibility with { BaselineRationale = "" } },
            Proposal() with { Compatibility = Proposal().Compatibility with { Source = "" } },
            Proposal() with { Compatibility = Proposal().Compatibility with { Binary = "" } },
            Proposal() with { Compatibility = Proposal().Compatibility with { Behavior = "" } },
            Proposal() with { Compatibility = Proposal().Compatibility with { Serialization = "" } },
            Proposal() with { Scope = "shared" },
            Proposal() with { After = "" }
        })
        {
            var result = ErgonomicsReviewPipeline.Validate(input, review with { Decisions = [review.Decisions[0] with { Proposals = [proposal] }] }, _root);
            Assert.False(result.Valid);
            Assert.DoesNotContain("## Discover known activity types", ErgonomicsReviewPipeline.Render(input, result));
        }
        var shared = Proposal() with { Scope = "shared", SharedApiRationale = "A general-purpose extension point is needed; update the Core documentation." };
        Assert.True(ErgonomicsReviewPipeline.Validate(input, review with { Decisions = [review.Decisions[0] with { Proposals = [shared] }] }, _root).Valid);
    }

    [Theory]
    [InlineData("agents/missing.cs", 1, 2, null)]
    [InlineData("agents/src/Activity.cs", 0, 2, null)]
    [InlineData("agents/src/Activity.cs", 1, 99, null)]
    [InlineData("agents/src/Activity.cs", 1, 2, "invented quote")]
    public void RejectsInvalidSourceEvidence(string path, int start, int end, string? quote)
    {
        var input = Prepare();
        var review = Review(input);
        var proposal = Proposal() with { Evidence = [new(path, quote, start, end), Cite("teams-baseline"), Cite("teams-candidate")] };
        Assert.False(ErgonomicsReviewPipeline.Validate(input, review with { Decisions = [review.Decisions[0] with { Proposals = [proposal] }] }, _root).Valid);
    }

    [Fact]
    public void RejectsStaleIdentityAndSourceChanges()
    {
        var input = Prepare();
        Assert.False(ErgonomicsReviewPipeline.Validate(input, Review(input) with { ReviewId = "stale" }, _root).Valid);
        File.AppendAllText(Path.Combine(_root, "teams-baseline/src/Activity.cs"), "changed");
        Assert.False(ErgonomicsReviewPipeline.Validate(input, Review(input), _root).Valid);
    }

    [Fact]
    public void ParserRejectsUnknownAndMissingFields()
    {
        var input = Prepare();
        var json = JsonSerializer.Serialize(Review(input), ToolJson.Options);
        Assert.Equal(input.ReviewId, ErgonomicsReviewPipeline.Parse(json).ReviewId);
        Assert.Throws<JsonException>(() => ErgonomicsReviewPipeline.Parse(json.Replace("\"problem\":", "\"unknown\":")));
        Assert.Throws<JsonException>(() => ErgonomicsReviewPipeline.Parse("{\"schemaVersion\":1}"));
        var legacyReview = JsonNode.Parse(json)!;
        legacyReview["decisions"]![0]!.AsObject().Remove("scenarios");
        Assert.Throws<JsonException>(() => ErgonomicsReviewPipeline.Parse(legacyReview.ToJsonString()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

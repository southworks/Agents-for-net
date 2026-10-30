using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class ErgonomicsReviewTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ergonomics-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly ErgonomicsCatalog Catalog = new(1, [new("discoverability", "Discover known values.")],
        [new("known-values", "discoverability", "Choose a known value.",
            [Surface("local", "agents/src/Activity.cs"), Surface("upstream", "teams-baseline/src/Activity.cs")])]);
    private static readonly ErgonomicsCatalog ResponseCatalog = new(1, [new("routing-and-helpers", "Return query results.")],
        [new("query-response", "routing-and-helpers", "Return an upstream builder result from a local handler.",
            [Surface("response-construction", "teams-baseline/src/Response.cs", "ResponseFactory.Build"), Surface("upstream-handler", "teams-baseline/src/Handlers.cs", "QueryHandler"),
             Surface("local-handler", "agents/src/Handlers.cs", "QueryHandler"), Surface("dispatcher", "agents/src/Dispatcher.cs", "Dispatcher.Dispatch")], ResponseChain: true)]);
    private const string Props = "<Project><ItemGroup><PackageVersion Include=\"Microsoft.Teams.Apps\" Version=\"2.1.2\" /></ItemGroup></Project>";

    private ErgonomicsInput Prepare(string candidate = "2.3.0", ErgonomicsCatalog? catalog = null) => ErgonomicsReviewPipeline.Prepare(_root, catalog ?? Catalog,
        new[] { "agents", "teams-baseline", "teams-candidate" }.Select(role =>
            (new BehaviorSource(role, role == "teams-candidate" ? candidate : "2.1.2", "https://github.com/microsoft/teams.net", new string('a', 40)),
            new Dictionary<string, string>
            {
                ["src/Activity.cs"] = "public class Activity { public string Type { get; set; } }\n// Known values\n",
                ["src/Response.cs"] = "public class ResponseFactory { public InvokeResponse<MessageExtensionResponse> Build() => new(200, response); }\n// Factory returns an envelope\n",
                ["src/Handlers.cs"] = role == "agents"
                    ? "public delegate Task<MessageExtensionResponse> QueryHandler();\n// Local handler returns a body\n"
                    : "public delegate Task<InvokeResponse<MessageExtensionResponse>> QueryHandler();\n// Upstream handler returns an envelope\n",
                ["src/Dispatcher.cs"] = "public class Dispatcher { public async Task Dispatch() { var result = await handler();\nawait SetResponse(context, result, status: 200); } }\n",
                ["Directory.Packages.props"] = Props
            })).ToArray());

    private static ErgonomicsSurface Surface(string id, string path, string symbol = "Activity") =>
        new(id, path.Split('/')[0], "Resolve this API role in the current snapshot.", [symbol], [path]);

    private static ReviewCitation Cite(string role) => new(role + "/src/Activity.cs", StartLine: 1, EndLine: 2);
    private static ErgonomicsProposal Proposal() => new("Discover known activity types", "IActivity.Type", "MSTeams", "msteams",
        "Known values are hard to find.", "Document existing constants at the entry point.",
        "activity.Type = \"message\";", "activity.Type = ActivityTypes.Message;", "Discover known values through documentation.", "candidate",
        new("Existing signatures remain.", "Existing compiled consumers remain valid.", "Custom strings remain valid.",
            "The property still serializes as a string.", "Uses existing constants available with the declared dependency 2.1.2."),
        "Compile old/new examples and verify custom string round-trips.",
        [Cite("agents"), Cite("teams-baseline"), Cite("teams-candidate")]);

    private static ErgonomicsReview Review(ErgonomicsInput input, string outcome = "recommendations") =>
        new(1, input.ReviewId, input.Catalog.Checks.Select(check => new ErgonomicsDecision(check.Id, outcome,
            "Inspected the cataloged caller tasks and comparison surfaces.",
            outcome == "recommendations" ? [Proposal()] : [], [Cite("agents")],
            input.Catalog.Operations.Where(o => o.CheckId == check.Id).Select(o => Scenario() with { OperationId = o.Id }).ToArray())).ToArray(),
            input.Catalog.Operations.Select(o => Coverage(input, o)).ToArray(), [],
            input.Navigation.DiscoveredOperations.Select(d => new ErgonomicsDiscoveryAssessment(d.Id, input.Catalog.Checks[0].Id,
                "cataloged", [input.Catalog.Operations[0].Id], "Compared the discovered registration with this caller task.", d.Evidence)).ToArray());

    private static ErgonomicsOperationCoverage Coverage(ErgonomicsInput input, ErgonomicsOperation operation) => new(operation.Id, "inspected",
        "Compared the required caller surfaces.", operation.Surfaces.Select(s => new ErgonomicsSurfaceEvidence(s.Id,
            [new(s.PathHints[0], StartLine: 1, EndLine: 2)], "Resolved the actual source declaration.",
            input.Navigation.Surfaces.Single(r => r.OperationId == operation.Id && r.SurfaceId == s.Id).Candidates
                .Select(c => c.Symbol).Distinct().ToArray())).ToArray(), operation.ResponseChain
            ? operation.Surfaces.Where(s => s.Id is "response-construction" or "upstream-handler" or "local-handler" or "dispatcher")
                .Select(s => new ErgonomicsResponseStep(s.Id, s.Id switch
                {
                    "response-construction" => "Build returns InvokeResponse<MessageExtensionResponse> with status 200 and a response body.",
                    "upstream-handler" => "The upstream delegate accepts Task<InvokeResponse<MessageExtensionResponse>>.",
                    "local-handler" => "The local delegate accepts Task<MessageExtensionResponse>; the envelope is not directly returnable.",
                    _ => "The dispatcher forwards the handler result as the body with status 200."
                }, [new(s.PathHints[0], StartLine: 1, EndLine: 2)])).ToArray()
            : []);

    private static ErgonomicsAlternative Alternative() => new("documentation", "activity.Type = ActivityTypes.Message;",
        "activity.Type = ActivityTypes.Message;", Proposal().Compatibility,
        "The existing entry-point documentation already links these constants; repeating the link adds no caller benefit.");

    private static ErgonomicsScenario Scenario() => new("Find a known activity type while composing a message.",
        "The caller assigns a string and needs to find the existing named constants.",
        "Link the existing constants from the caller's entry point rather than adding another value type.",
        "Evaluate the documentation path while retaining custom strings.", [Cite("agents")], "known-values", Alternative());

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
        Assert.Equal("public class Activity { public string Type { get; set; } }\n// Known values", result.Decisions[0].Proposals[0].Evidence[0].Quote);
        Assert.Equal("public class Activity { public string Type { get; set; } }\n// Known values", result.Decisions[0].Scenarios[0].Evidence[0].Quote);
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
        Assert.Equal("public class Activity { public string Type { get; set; } }", result.Decisions[0].Scenarios[0].Evidence[0].Quote);
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
        var missingScenarios = JsonNode.Parse(json)!;
        missingScenarios["decisions"]![0]!.AsObject().Remove("scenarios");
        Assert.Throws<JsonException>(() => ErgonomicsReviewPipeline.Parse(missingScenarios.ToJsonString()));
        var missingCoverage = JsonNode.Parse(json)!;
        missingCoverage.AsObject().Remove("coverage");
        Assert.Throws<JsonException>(() => ErgonomicsReviewPipeline.Parse(missingCoverage.ToJsonString()));
        foreach (var field in new[] { "additionalOperations", "discovery" })
        {
            var incomplete = JsonNode.Parse(json)!;
            incomplete.AsObject().Remove(field);
            Assert.Throws<JsonException>(() => ErgonomicsReviewPipeline.Parse(incomplete.ToJsonString()));
        }
        Assert.False(ErgonomicsReviewPipeline.Validate(input, Review(input) with { SchemaVersion = 99 }, _root).Valid);
    }

    [Fact]
    public void MissingDuplicateAndInventedOperationsCannotCountAsCoverage()
    {
        var input = Prepare();
        var review = Review(input);
        foreach (var coverage in new IReadOnlyList<ErgonomicsOperationCoverage>[]
        {
            [], [review.Coverage[0], review.Coverage[0]], [review.Coverage[0] with { OperationId = "invented" }],
            [review.Coverage[0] with { Status = "complete" }], [review.Coverage[0] with { Reason = "" }]
        })
        {
            Assert.False(ErgonomicsReviewPipeline.Validate(input, review with { Coverage = coverage }, _root).Valid);
        }
    }

    [Fact]
    public void AnInspectedOperationMustCiteEveryNamedComparisonSurface()
    {
        var input = Prepare();
        var review = Review(input);
        var entry = review.Coverage[0];
        foreach (var surfaces in new IReadOnlyList<ErgonomicsSurfaceEvidence>[]
        {
            [], [entry.Surfaces[0]], [entry.Surfaces[0], entry.Surfaces[0]],
            [entry.Surfaces[0], entry.Surfaces[1] with { Evidence = [Cite("agents")] }],
            [entry.Surfaces[0], entry.Surfaces[1] with { Evidence = [new("teams-baseline/src/Activity.cs", StartLine: 1, EndLine: 99)] }],
            [entry.Surfaces[0], entry.Surfaces[1] with { SurfaceId = "unrelated" }]
        })
        {
            var result = ErgonomicsReviewPipeline.Validate(input, review with { Coverage = [entry with { Surfaces = surfaces }] }, _root);
            Assert.False(result.Valid);
        }
        var valid = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.True(valid.Valid);
        Assert.Equal("public class Activity { public string Type { get; set; } }\n// Known values", valid.Coverage[0].Surfaces[0].Evidence[0].Quote);
    }

    [Theory]
    [InlineData("uninspected")]
    [InlineData("inconclusive")]
    public void PartialCoverageIsVisibleAndCannotSupportNoImprovement(string status)
    {
        var input = Prepare();
        var review = Review(input, "no-improvement");
        review = review with { Coverage = [review.Coverage[0] with
        {
            Status = status, Reason = "Upstream comparison remains unavailable.", Surfaces = []
        }] };
        var invalid = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.False(invalid.Valid);
        Assert.Contains(invalid.Errors, e => e.Contains("No-improvement requires inspected coverage"));
        review = review with { Decisions = [review.Decisions[0] with { Outcome = "inconclusive" }] };
        var valid = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.True(valid.Valid);
        var report = ErgonomicsReviewPipeline.Render(input, valid);
        Assert.Contains("0 of 1 required operations inspected", report);
        Assert.Contains($"| known-values | {status} | Upstream comparison remains unavailable.", report);
        Assert.Contains("Review limitation", report);
    }

    [Fact]
    public void RecommendationsRetainUninspectedOperationLimitations()
    {
        var catalog = Catalog with { Operations = [Catalog.Operations[0], Catalog.Operations[0] with { Id = "other-task" }] };
        var input = Prepare(catalog: catalog);
        var review = Review(input);
        review = review with { Coverage = [review.Coverage[0], review.Coverage[1] with
        {
            Status = "uninspected", Reason = "The second caller task was not evaluated.", Surfaces = []
        }] };
        var result = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.True(result.Valid);
        Assert.True(result.HasRecommendations);
        var report = ErgonomicsReviewPipeline.Render(input, result);
        Assert.Contains("1 of 2 required operations inspected", report);
        Assert.Contains("other-task | uninspected", report);
        Assert.Contains("Discover known activity types", report);
    }

    [Fact]
    public void OneScenarioCannotStandInForOtherInspectedOperations()
    {
        var catalog = Catalog with { Operations = [Catalog.Operations[0], Catalog.Operations[0] with { Id = "other-task" }] };
        var input = Prepare(catalog: catalog);
        var review = Review(input, "no-improvement");
        var decision = review.Decisions[0];
        review = review with { Decisions = [decision with { Scenarios = [decision.Scenarios[0]] }] };
        var result = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("own caller assessment: other-task"));
        review = review with { Decisions = [decision with { Scenarios = [decision.Scenarios[0] with { OperationId = "unknown" }] }] };
        Assert.False(ErgonomicsReviewPipeline.Validate(input, review, _root).Valid);
    }

    [Theory]
    [InlineData("response-construction")]
    [InlineData("upstream-handler")]
    [InlineData("local-handler")]
    [InlineData("dispatcher")]
    public void ResponseInteroperabilityRequiresEveryStageEvenWhenOtherSurfacesAreCited(string omittedStage)
    {
        var input = Prepare(catalog: ResponseCatalog);
        var review = Review(input, "no-improvement");
        var entry = review.Coverage[0];
        var valid = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.True(valid.Valid, string.Join("\n", valid.Errors));
        Assert.Contains("Build returns InvokeResponse", ErgonomicsReviewPipeline.Render(input, valid));
        Assert.Contains("public InvokeResponse<MessageExtensionResponse>", valid.Coverage[0].ResponseChain[0].Evidence[0].Quote);
        review = review with { Coverage = [entry with { ResponseChain = entry.ResponseChain.Where(s => s.Stage != omittedStage).ToArray() }] };
        var result = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("complete response chain"));
    }

    [Fact]
    public void ResponseStagesCannotSubstituteDeclarationsFromTheWrongContract()
    {
        var input = Prepare(catalog: ResponseCatalog);
        var review = Review(input);
        var entry = review.Coverage[0];
        var builder = entry.ResponseChain[0];
        foreach (var stage in new[]
        {
            builder with { Evidence = [new("agents/src/Handlers.cs", StartLine: 1, EndLine: 2)] },
            builder with { Evidence = [] }, builder with { Description = "" }, builder with { Stage = "unknown" }
        })
        {
            var result = ErgonomicsReviewPipeline.Validate(input,
                review with { Coverage = [entry with { ResponseChain = [stage, .. entry.ResponseChain.Skip(1)] }] }, _root);
            Assert.False(result.Valid);
        }
        Assert.False(ErgonomicsReviewPipeline.Validate(input,
            review with { Coverage = [entry with { ResponseChain = [.. entry.ResponseChain, builder] }] }, _root).Valid);
    }

    [Fact]
    public void RejectingOnlyABreakingAlternativeCannotJustifyNoImprovement()
    {
        var input = Prepare();
        var review = Review(input, "no-improvement");
        var scenario = review.Decisions[0].Scenarios[0];
        foreach (var alternative in new ErgonomicsAlternative?[]
        {
            null, Alternative() with { Kind = "breaking-change" }, Alternative() with { CurrentCallerCode = "" },
            Alternative() with { ProposedCallerCode = "" }, Alternative() with { BenefitAssessment = "" },
            Alternative() with { Compatibility = Proposal().Compatibility with { Source = "" } }
        })
        {
            var decision = review.Decisions[0] with { Scenarios = [scenario with { Alternative = alternative }] };
            var result = ErgonomicsReviewPipeline.Validate(input, review with { Decisions = [decision] }, _root);
            Assert.False(result.Valid);
            Assert.Contains(result.Errors, e => e.Contains("compatible alternative"));
        }
        foreach (var kind in new[] { "documentation", "additive-api", "existing-path" })
        {
            var decision = review.Decisions[0] with { Scenarios = [scenario with { Alternative = Alternative() with { Kind = kind } }] };
            Assert.True(ErgonomicsReviewPipeline.Validate(input, review with { Decisions = [decision] }, _root).Valid);
        }
    }

    [Fact]
    public void CatalogRequiresOperationCoverageForEveryAreaAndValidResponseSurfaces()
    {
        foreach (var catalog in new[]
        {
            Catalog with { Operations = [] },
            Catalog with { Operations = [Catalog.Operations[0], Catalog.Operations[0]] },
            Catalog with { Operations = [Catalog.Operations[0] with { CheckId = "unknown" }] },
            Catalog with { Operations = [Catalog.Operations[0] with { ResponseChain = true }] },
            Catalog with { Operations = [Catalog.Operations[0] with { Surfaces = [new("bad", "agents", "Invalid hint.", [], ["agents/../secret"])] }] }
        })
        {
            Assert.Throws<InvalidDataException>(() => Prepare(catalog: catalog));
        }
    }

    [Fact]
    public void ShippedCatalogRequiresResponseFlowsAndBothRegistrationSurfaces()
    {
        var catalog = ToolJson.Read<ErgonomicsCatalog>(Path.Combine(AppContext.BaseDirectory, "ergonomics.json"));
        Prepare(catalog: catalog);
        Assert.All(catalog.Checks, check => Assert.Contains(catalog.Operations, o => o.CheckId == check.Id));
        Assert.Equal(new[] { "fetch-action-response", "query-response", "settings-submit-response", "settings-url-response",
            "submit-action-response", "task-fetch-response", "task-submit-response" },
            catalog.Operations.Where(o => o.ResponseChain).Select(o => o.Id).Order().ToArray());
        var priority = Assert.Single(catalog.Operations, o => o.Id == "priority-and-authentication");
        Assert.Contains(priority.Surfaces, s => s.PathHints.Any(path => path.EndsWith("/App/TeamsAppExtensions.cs", StringComparison.Ordinal)));
        Assert.Contains(priority.Surfaces, s => s.PathHints.Any(path => path.EndsWith("/MessageExtensions/MessageExtension.cs", StringComparison.Ordinal)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

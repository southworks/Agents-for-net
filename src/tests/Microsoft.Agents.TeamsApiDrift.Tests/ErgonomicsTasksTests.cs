using System.Text.Json;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class ErgonomicsTasksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ergonomics-tasks-" + Guid.NewGuid().ToString("N"));
    private static readonly ErgonomicsCompatibility Compatibility = new("No existing signatures change.", "No binary contracts change.",
        "Runtime behavior remains unchanged.", "Wire shapes remain unchanged.", "Documentation uses declared Microsoft.Teams.Apps 2.1.2 APIs.");

    private ErgonomicsInput Prepare(int registrations = 1)
    {
        var checks = new[] { new BehaviorCheck("docs", "Read the API examples."), new BehaviorCheck("values", "Choose a value.") };
        var operations = Enumerable.Range(1, 5).Select(i => new ErgonomicsOperation("operation-" + i, i < 5 ? "docs" : "values", "Use this API.",
            [new("local", "agents", "Read local declarations.", ["Microsoft.Agents.Extensions.MSTeams.Routes"], []),
             new("baseline", "teams-baseline", "Compare upstream.", ["Microsoft.Teams.Apps.Routes"], [])])).ToArray();
        var sources = new[] { "agents", "teams-baseline", "teams-candidate" }.Select(role =>
        {
            var package = role == "agents" ? "Microsoft.Agents.Extensions.MSTeams" : "Microsoft.Teams.Apps";
            return (new BehaviorSource(role, "2.1.2", "fixture", new string('a', 40)), new Dictionary<string, string>
            {
                ["Directory.Packages.props"] = "<Project><ItemGroup><PackageVersion Include=\"Microsoft.Teams.Apps\" Version=\"2.1.2\" /></ItemGroup></Project>",
                ["Project.csproj"] = $"<Project><PropertyGroup><PackageId>{package}</PackageId></PropertyGroup></Project>",
                ["Moved/Api.cs"] = $"namespace {package}; public class Routes {{ " +
                    string.Join(" ", Enumerable.Range(1, registrations).Select(i => $"public void OnQuery{i}() {{ }}")) + " }\n"
            });
        }).ToArray();
        return ErgonomicsReviewPipeline.Prepare(_root, new(1, checks, operations), sources);
    }

    private static ErgonomicsReview Empty(ErgonomicsInput input) => new(1, input.ReviewId, [], [], [], []);

    private static ErgonomicsReview Discovery(ErgonomicsInput input) => Empty(input) with
    {
        Discovery = input.Navigation.DiscoveredOperations.Select(d => new ErgonomicsDiscoveryAssessment(d.Id, "docs", "cataloged",
            ["operation-1"], "The registration implements this caller task.", d.Evidence)).ToArray()
    };

    private static ErgonomicsReview Caller(ErgonomicsInput input, ErgonomicsTask task, string outcome = "no-improvement")
    {
        var evidence = task.Bindings.First(b => b.Candidates.Any(c => c.Evidence.Path.StartsWith("agents/", StringComparison.Ordinal))).Candidates[0].Evidence;
        var scenarios = task.Operations.Select(o => new ErgonomicsScenario("Register the query.", "Call OnQuery.", "Clarify the existing example.",
            "The documented caller path already provides the same information.", [evidence], o.Id,
            new("documentation", "routes.OnQuery();", "routes.OnQuery();", Compatibility, "Repeating the existing example adds no benefit."))).ToArray();
        return new(1, input.ReviewId, [new(task.Checks[0].Id, outcome, "Assessed the assigned caller tasks.",
            outcome == "recommendations" ? [new("Clarify the query example", "Routes.OnQuery", "MSTeams", "msteams",
                "The example omits a step.", "Document that step.", "routes.OnQuery();", "routes.OnQuery();", "The caller sees the required setup.",
                "agents", Compatibility, "Compile the example.", [evidence])] : [], [evidence], scenarios)],
            task.Operations.Select(o => new ErgonomicsOperationCoverage(o.Id, "inspected", "Compared the required surfaces.",
                o.Surfaces.Select(s =>
                {
                    var candidates = task.Bindings.Single(b => b.OperationId == o.Id && b.SurfaceId == s.Id).Candidates;
                    return new ErgonomicsSurfaceEvidence(s.Id, candidates.Select(c => c.Evidence).ToArray(), "Read the source contract.", candidates.Select(c => c.Symbol).ToArray());
                }).ToArray(), [])).ToArray(), [], []);
    }

    [Fact]
    public void TasksAreDeterministicBoundedAndKeepCompleteCoverage()
    {
        var input = Prepare();
        var first = ErgonomicsTasks.Plan(input, "operations", _root);
        var second = ErgonomicsTasks.Plan(input, "operations", _root);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(3, first.Count);
        Assert.All(first, t => { Assert.InRange(t.Operations.Count, 1, 3); Assert.Single(t.Checks); Assert.Empty(t.Registrations); });
        Assert.Equal(input.Catalog.Operations.Select(o => o.Id), first.SelectMany(t => t.Operations).Select(o => o.Id));
        var discovery = ErgonomicsTasks.Plan(input, "discovery", _root);
        Assert.Equal(input.Navigation.DiscoveredOperations.Count, discovery.Sum(t => t.Registrations.Count));
        Assert.All(discovery.SelectMany(t => t.Registrations), r => Assert.Contains("operation-1", r.SuggestedOperationIds));
        Assert.All(discovery, t => { Assert.InRange(t.Registrations.Count, 1, 20); Assert.Empty(t.Bindings); });
    }

    [Fact]
    public void OmittedMechanicalFieldsExpandWithoutInventingInspectionOrLosingExplicitEvidence()
    {
        var input = Prepare();
        var task = ErgonomicsTasks.Plan(input, "operations", _root)[0];
        var node = JsonSerializer.SerializeToNode(Caller(input, task), ToolJson.Options)!;
        foreach (var operation in node["coverage"]!.AsArray())
        foreach (var surface in operation!["surfaces"]!.AsArray())
        {
            surface!.AsObject().Remove("resolvedSymbols");
            surface.AsObject().Remove("evidence");
        }
        var expanded = ErgonomicsTasks.Expand(input, task, node.ToJsonString());
        Assert.All(expanded.Coverage.SelectMany(c => c.Surfaces), s => { Assert.NotEmpty(s.ResolvedSymbols); Assert.NotEmpty(s.Evidence); });
        Assert.True(ErgonomicsTasks.ValidateTask(input, task, expanded, _root, Discovery(input)).Valid);
        node["coverage"]![0]!["status"] = "uninspected";
        var uninspected = ErgonomicsTasks.Expand(input, task, node.ToJsonString());
        Assert.Equal("uninspected", uninspected.Coverage[0].Status);
        node["coverage"]![0]!["surfaces"]![0]!["evidence"] = JsonSerializer.SerializeToNode(new[] { new ReviewCitation("agents/unknown.cs", StartLine: 1, EndLine: 1) }, ToolJson.Options);
        Assert.False(ErgonomicsTasks.ValidateTask(input, task, ErgonomicsTasks.Expand(input, task, node.ToJsonString()), _root, Discovery(input)).Valid);
    }

    [Fact]
    public void AcceptedIndexedEvidenceRoundTripsThroughStrictReaderAndMerge()
    {
        var input = Prepare();
        var task = ErgonomicsTasks.Plan(input, "operations", _root)[0];
        var node = JsonSerializer.SerializeToNode(Caller(input, task, "recommendations"), ToolJson.Options)!;
        var reference = task.Bindings.Single(b => b.OperationId == task.Operations[0].Id && b.SurfaceId == "local").Candidates[0].Reference;
        node["decisions"]![0]!["evidence"] = JsonNodeForReference(reference!);
        node["decisions"]![0]!["proposals"]![0]!["evidence"] = JsonNodeForReference(reference!);
        var expanded = ErgonomicsTasks.Expand(input, task, node.ToJsonString());
        var accepted = ErgonomicsReviewPipeline.Parse(JsonSerializer.Serialize(expanded, ToolJson.Options));
        var result = ErgonomicsTasks.ValidateTask(input, task, accepted, _root, Discovery(input));
        Assert.True(result.Valid, string.Join("\n", result.Errors));
        Assert.All(accepted.Decisions.SelectMany(d => d.Evidence.Concat(d.Proposals.SelectMany(p => p.Evidence))),
            c => Assert.StartsWith("agents/", c.Path));
    }

    private static System.Text.Json.Nodes.JsonNode JsonNodeForReference(string id) =>
        System.Text.Json.Nodes.JsonNode.Parse("[{\"reference\":\"" + id + "\"}]")!;

    [Fact]
    public void DiscoveryEvidenceIsSuppliedFromEveryVersionAndSemanticMappingIsStillRequired()
    {
        var input = Prepare();
        var task = ErgonomicsTasks.Plan(input, "discovery", _root)[0];
        var node = JsonSerializer.SerializeToNode(Discovery(input), ToolJson.Options)!;
        foreach (var item in node["discovery"]!.AsArray()) item!.AsObject().Remove("evidence");
        var review = ErgonomicsTasks.Expand(input, task, node.ToJsonString());
        Assert.All(review.Discovery, d => Assert.Equal(input.Navigation.DiscoveredOperations.Single(r => r.Id == d.DiscoveryId).Evidence, d.Evidence));
        Assert.True(ErgonomicsTasks.ValidateTask(input, task, review, _root, null).Valid);
        var wrong = review with { Discovery = review.Discovery.Select(d => d with { OperationIds = ["unrelated-nonexistent-task"] }).ToArray() };
        Assert.False(ErgonomicsTasks.ValidateTask(input, task, wrong, _root, null).Valid);
    }

    [Fact]
    public void LargeRegistrationInventoriesArePartitionedWithoutDroppingVersionsOrEntries()
    {
        var input = Prepare(registrations: 25);
        var tasks = ErgonomicsTasks.Plan(input, "discovery", _root);
        Assert.Equal(3, tasks.Count);
        Assert.Equal(input.Navigation.DiscoveredOperations.Select(d => d.Id), tasks.SelectMany(t => t.Registrations).Select(r => r.Registration.Id));
        Assert.All(tasks, t => Assert.InRange(t.Registrations.Count, 1, 20));
    }

    [Fact]
    public void NewlyDiscoveredTasksAreScheduledAndValidatedAlongsideSeededTasks()
    {
        var input = Prepare();
        var discovery = Discovery(input);
        var additional = input.Catalog.Operations[0] with { Id = "new-version-task" };
        discovery = discovery with
        {
            AdditionalOperations = [additional],
            Discovery = [discovery.Discovery[0] with { Disposition = "additional", OperationIds = [additional.Id] }, .. discovery.Discovery.Skip(1)]
        };
        var tasks = ErgonomicsTasks.Plan(input, "operations", _root, discovery);
        Assert.Contains(tasks, t => t.Operations.Any(o => o.Id == additional.Id));
        var parts = tasks.Select(t => Caller(input, t)).Prepend(discovery).ToArray();
        var result = ErgonomicsReviewPipeline.Validate(input, ErgonomicsTasks.Merge(input, parts), _root);
        Assert.True(result.Valid, string.Join("\n", result.Errors));
        Assert.True(result.AnalysisComplete);
        Assert.Equal(6, result.Coverage.Count);
    }

    [Fact]
    public void CompleteMergedReviewPassesOriginalValidatorWithOrWithoutRecommendations()
    {
        var input = Prepare();
        var tasks = ErgonomicsTasks.Plan(input, "operations", _root);
        foreach (var outcome in new[] { "recommendations", "no-improvement" })
        {
            var parts = tasks.Select(t => Caller(input, t, outcome)).Prepend(Discovery(input)).ToArray();
            var merged = ErgonomicsTasks.Merge(input, parts);
            var result = ErgonomicsReviewPipeline.Validate(input, merged, _root);
            Assert.True(result.Valid, string.Join("\n", result.Errors));
            Assert.True(result.AnalysisComplete);
            Assert.Equal(outcome == "recommendations", result.HasRecommendations);
        }
    }

    [Fact]
    public void MissingOrInconclusiveChunksRetainFindingsAndFailCompletion()
    {
        var input = Prepare();
        var tasks = ErgonomicsTasks.Plan(input, "operations", _root);
        var found = Caller(input, tasks[0], "recommendations");
        foreach (var parts in new ErgonomicsReview[][]
        {
            [Discovery(input), found],
            [Discovery(input), found, Caller(input, tasks[1], "inconclusive"), Caller(input, tasks[2])]
        })
        {
            var result = ErgonomicsReviewPipeline.Validate(input, ErgonomicsTasks.Merge(input, parts), _root);
            Assert.True(result.Valid, string.Join("\n", result.Errors));
            Assert.True(result.HasRecommendations);
            Assert.False(result.AnalysisComplete);
            Assert.Contains(result.Coverage, c => c.Status is "uninspected" or "inconclusive");
        }
    }

    [Fact]
    public void TasksRejectWrongScopeIdentityDuplicatesAndIncompleteNegativeAlternatives()
    {
        var input = Prepare();
        var task = ErgonomicsTasks.Plan(input, "operations", _root)[0];
        var review = Caller(input, task);
        Assert.Throws<InvalidDataException>(() => ErgonomicsTasks.Expand(input, task, JsonSerializer.Serialize(review with { ReviewId = "stale" })));
        Assert.Throws<InvalidDataException>(() => ErgonomicsTasks.Expand(input, task, JsonSerializer.Serialize(review with { Coverage = [] })));
        Assert.Throws<InvalidDataException>(() => ErgonomicsTasks.Merge(input, [review, review]));
        Assert.Throws<InvalidDataException>(() => ErgonomicsTasks.ValidateTask(input, task with { Bindings = [] }, review, _root, Discovery(input)));
        review = review with { Decisions = [review.Decisions[0] with { Scenarios = review.Decisions[0].Scenarios.Select(s => s with { Alternative = null }).ToArray() }] };
        Assert.False(ErgonomicsTasks.ValidateTask(input, task, review, _root, Discovery(input)).Valid);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}

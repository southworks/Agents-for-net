using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class ErgonomicsVersionedSourcesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ergonomics-versions-" + Guid.NewGuid().ToString("N"));
    private const string Check = "routing-and-helpers";
    private static ErgonomicsSurface Surface(string id, string role, string symbol) =>
        new(id, role, "Resolve the actual API contract in this version.", [symbol], [role + "/old-layout/Removed.cs"]);
    private static readonly ErgonomicsOperation Query = new("query", Check, "Return a query response.",
        [Surface("response-construction", "teams-baseline", "Microsoft.Teams.Apps.Response.Create"),
         Surface("upstream-handler", "teams-baseline", "Microsoft.Teams.Apps.Handler"),
         Surface("local-handler", "agents", "Microsoft.Agents.Extensions.MSTeams.Handler"),
         Surface("dispatcher", "agents", "Microsoft.Agents.Extensions.MSTeams.Dispatcher.Send")], true);
    private static readonly ErgonomicsCatalog Catalog = new(1, [new(Check, "Return results and inspect registrations.")], [Query]);

    private ErgonomicsInput Prepare(ErgonomicsCatalog? catalog = null, bool renamedFactory = false, bool modernSyntax = false,
        bool changedNamespace = false, bool conditional = false)
    {
        var sources = new[] { "agents", "teams-baseline", "teams-candidate" }.Select(role =>
        {
            var ns = changedNamespace ? "VersionSpecific.Api" : role == "agents" ? "Microsoft.Agents.Extensions.MSTeams" : "Microsoft.Teams.Apps";
            var registration = role == "teams-baseline" ? "OnRemoved" : "OnAdded";
            var files = new Dictionary<string, string>
            {
                ["Directory.Packages.props"] = "<Project><ItemGroup><PackageVersion Include=\"Microsoft.Teams.Apps\" Version=\"2.1.2\" /></ItemGroup></Project>",
                ["PublishedProject.csproj"] = $"<Project><PropertyGroup><PackageId>{(role == "agents" ? "Microsoft.Agents.Extensions.MSTeams" : "Microsoft.Teams.Apps")}</PackageId></PropertyGroup></Project>",
                ["moved/Contracts.cs"] = $"namespace {ns}; public delegate Response Handler(); public partial class Response {{ }}\n",
                ["factories/Creation.cs"] = $"namespace {ns}; public partial class Response {{ public static Response {(renamedFactory ? "From" : "Create")}() => new(); }}\n",
                ["transport/Send.cs"] = $"namespace {ns}; public class Dispatcher {{ public void Send() {{ }} }}\n",
                ["registration/Routes.cs"] = $"namespace {ns}; public class Routes {{ public void OnQuery() {{ }} public void {registration}() {{ }} }}\n",
                ["registration/Overloads.cs"] = $"namespace {ns}; public class OtherRoutes {{ public void OnQuery(int value) {{ }} }}\n",
                ["registration/Decoys.cs"] = $"namespace {ns}; public class NonRoutes {{ private void OnPrivate() {{ }} public string Text => \"public void OnString() {{ }}\"; }} // public void OnComment() {{ }}\n",
                ["registration/PartialType.cs"] = $"namespace {ns}; public partial class SplitRoutes {{ }}\n",
                ["registration/PartialMethods.cs"] = $"namespace {ns}; partial class SplitRoutes {{ public void OnSplit() {{ }} }}\n",
                ["registration/Interface.cs"] = $"namespace {ns}; public interface IRegistration {{ void OnInterface(); }}\n",
                ["documentation/NewGuide.md"] = "# Current registration guide\n"
            };
            if (modernSyntax) files["new-syntax/Primary.cs"] = $"namespace {ns}; public class Primary(int value) {{ public void OnModern() {{ }} }}\n";
            if (conditional) files["conditional/Optional.cs"] = $"namespace {ns};\n#if OPTIONAL_API\npublic delegate Response OptionalHandler();\n#endif\n";
            return (new BehaviorSource(role, role == "teams-candidate" ? "9.0.0" : "2.1.2", "fixture", new string('a', 40)), files);
        }).ToArray();
        return ErgonomicsReviewPipeline.Prepare(_root, catalog ?? Catalog, sources);
    }

    private ErgonomicsOperationCoverage Coverage(ErgonomicsInput input, ErgonomicsOperation operation)
    {
        var index = ErgonomicsSourceIndex.Create(_root, input.Files);
        var surfaces = operation.Surfaces.Select(surface =>
        {
            var locations = index.Candidates(surface);
            var first = locations[0];
            return new ErgonomicsSurfaceEvidence(surface.Id, [first.Evidence], "Resolved the declaration in the current snapshot.", [first.Symbol]);
        }).ToArray();
        return new(operation.Id, "inspected", "Traced the actual construction and handler contracts.", surfaces,
            operation.ResponseChain ? surfaces.Select(s => new ErgonomicsResponseStep(s.SurfaceId,
                s.SurfaceId == "response-construction" ? "Create is a factory returning Response; this version has no builder." : "Compared this response contract.", s.Evidence)).ToArray() : []);
    }

    private ErgonomicsReview Review(ErgonomicsInput input)
    {
        var evidence = input.Navigation.DiscoveredOperations.First(d => d.Availability == "agents").Evidence;
        var compatible = new ErgonomicsCompatibility("No signatures change.", "No compiled contracts change.",
            "Existing behavior remains.", "Existing wire shape remains.", "Uses the declared 2.1.2 API.");
        return new(1, input.ReviewId,
            [new(Check, "no-improvement", "Compared the cataloged caller tasks.", [], evidence,
                [new("Return results.", "Use the existing factory.", "Document the existing construction path.",
                    "The existing documentation provides the same caller path.", evidence, "query",
                    new("documentation", "Response.Create()", "Response.Create()", compatible, "Repeating existing guidance adds no caller benefit."))])],
            [Coverage(input, input.Catalog.Operations.First(o => o.Id == "query"))], [], input.Navigation.DiscoveredOperations.Select(d =>
                new ErgonomicsDiscoveryAssessment(d.Id, Check, "cataloged", ["query"], "Compared this registration with the query task.", d.Evidence)).ToArray());
    }

    [Fact]
    public void MovedAndSplitDeclarationsResolveWithoutTheOldPaths()
    {
        var input = Prepare();
        Assert.All(input.Navigation.Surfaces, s => Assert.NotEmpty(s.Candidates));
        Assert.DoesNotContain(input.Files, f => f.Path.Contains("old-layout", StringComparison.Ordinal));
        var result = ErgonomicsReviewPipeline.Validate(input, Review(input), _root);
        Assert.True(result.Valid, string.Join("\n", result.Errors));
        Assert.Contains("factories/Creation.cs", result.Coverage[0].ResponseChain[0].Evidence[0].Path);
        Assert.Contains("this version has no builder", ErgonomicsReviewPipeline.Render(input, result));
    }

    [Fact]
    public void RenamedFactoriesCanBeExplicitlyResolvedToTheirCurrentDeclarations()
    {
        var input = Prepare(renamedFactory: true);
        var construction = Query.Surfaces[0] with { Symbols = ["Microsoft.Teams.Apps.Response.From"] };
        var current = Query with { Surfaces = [construction, .. Query.Surfaces.Skip(1)] };
        var review = ReviewWithCoverage(input, Coverage(input, current));
        var result = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.True(result.Valid, string.Join("\n", result.Errors));
        Assert.Contains("Response.From", result.Coverage[0].Surfaces[0].ResolvedSymbols[0]);
    }

    private ErgonomicsReview ReviewWithCoverage(ErgonomicsInput input, ErgonomicsOperationCoverage coverage)
    {
        // Build the shared assessment without assuming the catalog's old symbol still exists.
        var replacement = Query with { Surfaces = Query.Surfaces.Select(s => s.Id == "response-construction"
            ? s with { Symbols = ["Microsoft.Teams.Apps.Response.From"] } : s).ToArray() };
        var temporaryInput = input with { Catalog = Catalog with { Operations = [replacement] } };
        return Review(temporaryInput) with { ReviewId = input.ReviewId, Coverage = [coverage] };
    }

    [Fact]
    public void ExistingSurfacesCannotBeDismissedAsNotApplicable()
    {
        var input = Prepare();
        var review = Review(input);
        var entry = review.Coverage[0];
        var surface = entry.Surfaces[0] with { Status = "not-applicable", Resolution = "Claimed absent, despite its current declaration." };
        var result = ErgonomicsReviewPipeline.Validate(input, review with
        {
            Coverage = [entry with { Surfaces = [surface, .. entry.Surfaces.Skip(1)] }]
        }, _root);
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("comparison surface"));
    }

    [Fact]
    public void AbsentOperationsNeedPositiveSourceEvidenceAndAnExplicitAbsentSurface()
    {
        var retired = new ErgonomicsOperation("retired", Check, "Previously supported registration.",
            [Surface("retired-handler", "agents", "Microsoft.Agents.Extensions.MSTeams.RetiredHandler")]);
        var input = Prepare(Catalog with { Operations = [Query, retired] });
        var review = Review(input);
        var proof = input.Navigation.DiscoveredOperations.First(d => d.Availability == "agents").Evidence;
        var absent = new ErgonomicsSurfaceEvidence("retired-handler", proof,
            "The former handler is absent; the current registration declarations establish the remaining surface.", [], "not-applicable");
        var entry = new ErgonomicsOperationCoverage("retired", "not-applicable", "This operation is no longer present in the local SDK.", [absent], []);
        review = review with { Coverage = [review.Coverage[0], entry] };
        var valid = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.True(valid.Valid, string.Join("\n", valid.Errors));
        Assert.Contains("1 not applicable with source evidence", ErgonomicsReviewPipeline.Render(input, valid));
        Assert.False(ErgonomicsReviewPipeline.Validate(input,
            review with { Coverage = [review.Coverage[0], entry with { Surfaces = [] }] }, _root).Valid);
        Assert.False(ErgonomicsReviewPipeline.Validate(input,
            review with { Coverage = [review.Coverage[0], entry with { Surfaces = [absent with { Evidence = [] }] }] }, _root).Valid);
    }

    [Fact]
    public void CandidateAdditionsAndRemovalsAreDiscoveredBySymbolRatherThanFileName()
    {
        var input = Prepare();
        var routes = input.Navigation.DiscoveredOperations;
        Assert.Contains(routes, d => d.Symbol.EndsWith("Routes.OnAdded", StringComparison.Ordinal) && d.Availability == "candidate-added");
        Assert.Contains(routes, d => d.Symbol.EndsWith("Routes.OnRemoved", StringComparison.Ordinal) && d.Availability == "candidate-removed");
        Assert.Contains(routes, d => d.Symbol.EndsWith("Routes.OnQuery", StringComparison.Ordinal) && d.Availability == "baseline-and-candidate");
        Assert.Contains(routes, d => d.Symbol.EndsWith("OtherRoutes.OnQuery", StringComparison.Ordinal));
        Assert.Contains(routes, d => d.Symbol.EndsWith("SplitRoutes.OnSplit", StringComparison.Ordinal));
        Assert.Contains(routes, d => d.Symbol.EndsWith("IRegistration.OnInterface", StringComparison.Ordinal));
        var review = Review(input);
        var omitted = review.Discovery.Where(d => !d.DiscoveryId.EndsWith("Routes.OnRemoved", StringComparison.Ordinal)).ToArray();
        Assert.False(ErgonomicsReviewPipeline.Validate(input, review with { Discovery = omitted }, _root).Valid);
        var report = ErgonomicsReviewPipeline.Render(input, ErgonomicsReviewPipeline.Validate(input, review, _root));
        Assert.Contains("candidate-added", report);
        Assert.Contains("candidate-removed", report);
    }

    [Fact]
    public void NewOperationsExtendRequiredCoverageAndCannotBeHiddenByNoImprovement()
    {
        var input = Prepare();
        var review = Review(input);
        var found = input.Navigation.DiscoveredOperations.First(d => d.Availability == "candidate-added");
        var extra = new ErgonomicsOperation("new-registration", Check, "Inspect the new candidate registration.",
            [Surface("candidate-registration", "teams-candidate", found.Symbol), Query.Surfaces[2]]);
        var assessment = new ErgonomicsDiscoveryAssessment(found.Id, Check, "additional", [extra.Id],
            "This registration is not represented by a seeded caller task.", found.Evidence);
        review = review with
        {
            AdditionalOperations = [extra], Discovery = review.Discovery.Select(d => d.DiscoveryId == found.Id ? assessment : d).ToArray(),
            Coverage = [.. review.Coverage, new(extra.Id, "uninspected", "The newly discovered task needs further analysis.", [], [])]
        };
        Assert.False(ErgonomicsReviewPipeline.Validate(input, review, _root).Valid);
        review = review with { Decisions = [review.Decisions[0] with { Outcome = "inconclusive" }] };
        var result = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.True(result.Valid, string.Join("\n", result.Errors));
        Assert.Contains("new-registration | uninspected", ErgonomicsReviewPipeline.Render(input, result));
    }

    [Fact]
    public void ReconciliationRequiresEvidenceFromEveryPresentVersionAndCannotInventMappings()
    {
        var input = Prepare();
        var review = Review(input);
        var shared = review.Discovery.First(d => d.Evidence.Any(c => c.Path.StartsWith("teams-baseline/", StringComparison.Ordinal)) &&
            d.Evidence.Any(c => c.Path.StartsWith("teams-candidate/", StringComparison.Ordinal)));
        foreach (var changed in new[]
        {
            shared with { Evidence = shared.Evidence.Where(c => c.Path.StartsWith("teams-baseline/", StringComparison.Ordinal)).ToArray() },
            shared with { OperationIds = ["invented"] }, shared with { Reason = "" }
        })
            Assert.False(ErgonomicsReviewPipeline.Validate(input, review with
            {
                Discovery = review.Discovery.Select(d => d.DiscoveryId == shared.DiscoveryId ? changed : d).ToArray()
            }, _root).Valid);
    }

    [Fact]
    public void ModernSyntaxRetainsItsContainingTypeInDiscovery()
    {
        var input = Prepare(modernSyntax: true);
        Assert.Contains(input.Navigation.DiscoveredOperations, d => d.Symbol.EndsWith("Primary.OnModern", StringComparison.Ordinal));
        Assert.NotEmpty(input.Navigation.Limitations);
    }

    [Fact]
    public void ConditionalDeclarationsCannotBeUsedToEstablishAbsence()
    {
        var input = Prepare(conditional: true);
        var index = ErgonomicsSourceIndex.Create(_root, input.Files);
        var surface = Surface("optional", "teams-baseline", "Microsoft.Teams.Apps.OptionalHandler");
        Assert.Empty(index.Candidates(surface));
        Assert.False(index.CanEstablishAbsence(surface));
        Assert.Contains(input.Navigation.Limitations, l => l.Contains("conditional compilation"));
    }

    [Fact]
    public void CommentsStringsAndPrivateMethodsDoNotCreateDiscoveredOperations()
    {
        var input = Prepare();
        Assert.DoesNotContain(input.Navigation.DiscoveredOperations, d =>
            d.Symbol.Contains("OnPrivate", StringComparison.Ordinal) || d.Symbol.Contains("OnString", StringComparison.Ordinal) ||
            d.Symbol.Contains("OnComment", StringComparison.Ordinal));
    }

    [Fact]
    public void PackageProjectScopeDiscoversRegistrationsAfterANamespaceChange()
    {
        var input = Prepare(changedNamespace: true);
        Assert.Contains(input.Navigation.DiscoveredOperations, d => d.Symbol == "VersionSpecific.Api.Routes.OnAdded" && d.Availability == "candidate-added");
        Assert.Contains(input.Navigation.DiscoveredOperations, d => d.Symbol == "VersionSpecific.Api.Routes.OnQuery" && d.Availability == "agents");
    }

    [Fact]
    public void AFlowWithoutADedicatedUpstreamHandlerCanExplainThatStageAsNotApplicable()
    {
        var optional = Query.Surfaces[1] with { Symbols = ["Microsoft.Teams.Apps.RetiredDedicatedHandler"] };
        var operation = Query with { Surfaces = [Query.Surfaces[0], optional, .. Query.Surfaces.Skip(2)] };
        var input = Prepare(Catalog with { Operations = [operation] });
        var review = Review(input with { Catalog = Catalog });
        var proof = input.Navigation.DiscoveredOperations.First(d => d.Availability == "baseline-and-candidate").Evidence
            .Where(c => c.Path.StartsWith("teams-baseline/", StringComparison.Ordinal)).ToArray();
        var absent = new ErgonomicsSurfaceEvidence("upstream-handler", proof,
            "The dedicated delegate no longer exists; the current registration supplies the contract directly.", [], "not-applicable");
        var entry = review.Coverage[0];
        review = review with { Coverage = [entry with
        {
            Surfaces = entry.Surfaces.Select(s => s.SurfaceId == "upstream-handler" ? absent : s).ToArray(),
            ResponseChain = entry.ResponseChain.Select(s => s.Stage == "upstream-handler"
                ? new ErgonomicsResponseStep(s.Stage, absent.Resolution, proof, "not-applicable") : s).ToArray()
        }] };
        var result = ErgonomicsReviewPipeline.Validate(input, review, _root);
        Assert.True(result.Valid, string.Join("\n", result.Errors));
    }

    [Fact]
    public void DocumentationCanResolveToARenamedFileWithoutADeclaration()
    {
        var documentation = new ErgonomicsSurface("guide", "agents", "Read the current guide.", [], ["agents/old/Guide.md"]);
        var input = Prepare(Catalog with { Operations = [Query with { Surfaces = [.. Query.Surfaces, documentation] }] });
        var review = Review(input with { Catalog = Catalog });
        var entry = review.Coverage[0];
        review = review with { Coverage = [entry with { Surfaces = [.. entry.Surfaces,
            new("guide", [new("agents/documentation/NewGuide.md", StartLine: 1, EndLine: 1)],
                "The guide was renamed and moved in this checkout.", ["file:agents/documentation/NewGuide.md"])] }] };
        Assert.True(ErgonomicsReviewPipeline.Validate(input, review, _root).Valid);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

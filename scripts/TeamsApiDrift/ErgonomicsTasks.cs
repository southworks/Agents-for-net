using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record ErgonomicsRegistrationTask(ErgonomicsDiscoveredOperation Registration,
    IReadOnlyList<string> SuggestedOperationIds);

public sealed record ErgonomicsTask(int SchemaVersion, string ReviewId, string TaskId, string Kind,
    IReadOnlyList<BehaviorSource> Sources, IReadOnlyList<BehaviorCheck> Checks,
    IReadOnlyList<ErgonomicsOperation> Operations, IReadOnlyList<ErgonomicsSurfaceLocation> Bindings,
    IReadOnlyList<ErgonomicsRegistrationTask> Registrations, IReadOnlyList<string> Limitations);

// Task boundaries and citation bindings are deterministic. Inspection, scope exclusions,
// replacement mappings and compatibility conclusions remain the reviewer's responsibility.
internal static class ErgonomicsTasks
{
    internal static IReadOnlyList<ErgonomicsTask> Plan(ErgonomicsInput input, string phase, string workspace,
        ErgonomicsReview? discovery = null)
    {
        var tasks = new List<ErgonomicsTask>();
        ErgonomicsSourceIndex? index = null;
        var operations = input.Catalog.Operations.Concat(discovery?.AdditionalOperations ?? []).ToArray();
        if (phase == "discovery")
        {
            foreach (var batch in input.Navigation.DiscoveredOperations.Chunk(20))
            {
                var registrations = batch.Select(d => new ErgonomicsRegistrationTask(d,
                    input.Navigation.Surfaces.Where(s => s.Candidates.Any(c =>
                        (d.Symbol == c.Symbol || d.Symbol.StartsWith(c.Symbol + ".", StringComparison.Ordinal)) &&
                        d.Evidence.Any(e => ErgonomicsSourceIndex.Overlaps(e, c.Evidence))))
                        .Select(s => s.OperationId).Distinct(StringComparer.Ordinal).ToArray())).ToArray();
                tasks.Add(new(1, input.ReviewId, $"discovery-{tasks.Count + 1:D3}", phase, input.Sources,
                    input.Catalog.Checks, operations.Select(o => o with { Surfaces = [] }).ToArray(), [], registrations,
                    input.Navigation.Limitations));
            }
        }
        else if (phase == "operations")
        {
            foreach (var check in input.Catalog.Checks)
            foreach (var batch in operations.Where(o => o.CheckId == check.Id).Chunk(3))
            {
                var bindings = batch.SelectMany(o => o.Surfaces.Select(s =>
                    input.Navigation.Surfaces.SingleOrDefault(b => b.OperationId == o.Id && b.SurfaceId == s.Id) ??
                    new ErgonomicsSurfaceLocation(o.Id, s.Id, (index ??= ErgonomicsSourceIndex.Create(workspace, input.Files)).Candidates(s)))).ToArray();
                tasks.Add(new(1, input.ReviewId, $"operations-{tasks.Count + 1:D3}", phase, input.Sources, [check],
                    batch, bindings, [], input.Navigation.Limitations));
            }
        }
        else throw new ArgumentException("Task phase must be discovery or operations.");
        return tasks;
    }

    internal static ErgonomicsReview Expand(ErgonomicsInput input, ErgonomicsTask task, string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Empty task response.");
        // Fill only omitted mechanical fields. Explicit evidence/renamed bindings are
        // preserved for the normal source/role/symbol validator to check.
        foreach (var entry in node["discovery"]?.AsArray() ?? [])
        {
            var id = entry?["discoveryId"]?.GetValue<string>();
            var registration = task.Registrations.SingleOrDefault(r => r.Registration.Id == id)?.Registration;
            if (registration is not null && entry is JsonObject obj && !obj.ContainsKey("evidence"))
                obj["evidence"] = JsonSerializer.SerializeToNode(registration.Evidence, ToolJson.Options);
        }
        foreach (var entry in node["coverage"]?.AsArray() ?? [])
        {
            var operationId = entry?["operationId"]?.GetValue<string>();
            foreach (var surface in entry?["surfaces"]?.AsArray() ?? [])
            {
                var binding = task.Bindings.SingleOrDefault(b => b.OperationId == operationId &&
                    b.SurfaceId == surface?["surfaceId"]?.GetValue<string>());
                if (binding is null || binding.Candidates.Count == 0 || surface is not JsonObject obj ||
                    surface["status"]?.GetValue<string>() == "not-applicable") continue;
                if (!obj.ContainsKey("resolvedSymbols"))
                    obj["resolvedSymbols"] = JsonSerializer.SerializeToNode(binding.Candidates.Select(c => c.Symbol).Distinct().ToArray(), ToolJson.Options);
                if (!obj.ContainsKey("evidence"))
                    obj["evidence"] = JsonSerializer.SerializeToNode(binding.Candidates.Select(c => c.Evidence).ToArray(), ToolJson.Options);
            }
        }
        var review = ErgonomicsReviewPipeline.Parse(node.ToJsonString());
        if (review.SchemaVersion != 1 || review.ReviewId != input.ReviewId)
            throw new InvalidDataException("Task response identity or schema mismatch.");
        if (task.Kind == "discovery")
        {
            if (review.Decisions.Count != 0 || review.Coverage.Count != 0 ||
                !SameIds(review.Discovery.Select(d => d.DiscoveryId), task.Registrations.Select(r => r.Registration.Id)))
                throw new InvalidDataException("Discovery task must reconcile exactly its assigned registrations, with no caller decisions or coverage.");
        }
        else if (review.Discovery.Count != 0 || review.AdditionalOperations.Count != 0 ||
            !SameIds(review.Coverage.Select(c => c.OperationId), task.Operations.Select(o => o.Id)) ||
            !SameIds(review.Decisions.Select(d => d.CheckId), task.Checks.Select(c => c.Id)) ||
            review.Decisions.SelectMany(d => d.Scenarios).Any(s => !task.Operations.Any(o => o.Id == s.OperationId)))
            throw new InvalidDataException("Caller task must assess exactly its assigned operations and check; discovery and task definitions are supplied by the discovery phase.");
        // Accepted task JSON must round-trip through the strict public reader.
        // Its internal reference sentinel is never serialized as an explicit path.
        IReadOnlyList<ReviewCitation> Resolve(IReadOnlyList<ReviewCitation> citations) => citations.Select(c =>
            c.Path.StartsWith(ErgonomicsCitationConverter.ReferencePrefix, StringComparison.Ordinal)
                ? input.Navigation.IndexedCitations.SingleOrDefault(r => r.Id == c.Path[ErgonomicsCitationConverter.ReferencePrefix.Length..])?.Citation
                    ?? throw new InvalidDataException($"Unknown indexed citation reference: {c.Path}.")
                : c).ToArray();
        return review with
        {
            Coverage = review.Coverage.Select(c => c with
            {
                Surfaces = c.Surfaces.Select(s => s with { Evidence = Resolve(s.Evidence) }).ToArray(),
                ResponseChain = c.ResponseChain.Select(s => s with { Evidence = Resolve(s.Evidence) }).ToArray()
            }).ToArray(),
            Discovery = review.Discovery.Select(d => d with { Evidence = Resolve(d.Evidence) }).ToArray(),
            Decisions = review.Decisions.Select(d => d with
            {
                Evidence = Resolve(d.Evidence),
                Scenarios = d.Scenarios.Select(s => s with { Evidence = Resolve(s.Evidence) }).ToArray(),
                Proposals = d.Proposals.Select(p => p with { Evidence = Resolve(p.Evidence) }).ToArray()
            }).ToArray()
        };
    }

    private static bool SameIds(IEnumerable<string> actual, IEnumerable<string> expected)
    {
        var values = actual.ToArray();
        return values.Distinct(StringComparer.Ordinal).Count() == values.Length &&
            values.ToHashSet(StringComparer.Ordinal).SetEquals(expected);
    }

    internal static ErgonomicsReview Merge(ErgonomicsInput input, IReadOnlyList<ErgonomicsReview> parts)
    {
        if (parts.Any(p => p.SchemaVersion != 1 || p.ReviewId != input.ReviewId))
            throw new InvalidDataException("Cannot merge tasks from a different review.");
        if (parts.SelectMany(p => p.Decisions).Any(d => !input.Catalog.Checks.Any(c => c.Id == d.CheckId)))
            throw new InvalidDataException("Cannot merge an unknown check.");
        var additional = parts.SelectMany(p => p.AdditionalOperations).ToArray();
        var operations = input.Catalog.Operations.Concat(additional).ToArray();
        var submittedCoverage = parts.SelectMany(p => p.Coverage).ToArray();
        var submittedDiscovery = parts.SelectMany(p => p.Discovery).ToArray();
        if (operations.Select(o => o.Id).Distinct().Count() != operations.Length ||
            submittedCoverage.Select(c => c.OperationId).Distinct().Count() != submittedCoverage.Length ||
            submittedDiscovery.Select(d => d.DiscoveryId).Distinct().Count() != submittedDiscovery.Length ||
            submittedCoverage.Any(c => !operations.Any(o => o.Id == c.OperationId)) ||
            submittedDiscovery.Any(d => !input.Navigation.DiscoveredOperations.Any(r => r.Id == d.DiscoveryId)))
            throw new InvalidDataException("Duplicate or unassigned task coverage/discovery cannot be merged.");
        var unresolved = parts.SelectMany(p => p.Decisions).Where(d => d.Outcome == "inconclusive")
            .SelectMany(d => d.Scenarios.Select(s => s.OperationId)).ToHashSet(StringComparer.Ordinal);
        var coverage = operations.Select(o =>
        {
            var entry = submittedCoverage.SingleOrDefault(c => c.OperationId == o.Id) ??
                new(o.Id, "uninspected", "The assigned caller review did not produce accepted output.", [], []);
            // A recommendation from another chunk must not erase an unresolved
            // conclusion in this chunk merely because declarations were inspected.
            return unresolved.Contains(o.Id) && entry.Status == "inspected"
                ? entry with { Status = "inconclusive", Reason = entry.Reason + " The caller assessment remains inconclusive." }
                : entry;
        }).ToArray();
        var discovery = input.Navigation.DiscoveredOperations.Select(r => submittedDiscovery.SingleOrDefault(d => d.DiscoveryId == r.Id) ??
            new(r.Id, input.Catalog.Checks[0].Id, "inconclusive", [],
                "The assigned registration review did not produce accepted output.", r.Evidence)).ToArray();
        var decisions = input.Catalog.Checks.Select(check =>
        {
            var entries = parts.SelectMany(p => p.Decisions).Where(d => d.CheckId == check.Id).ToArray();
            var scenarios = entries.SelectMany(d => d.Scenarios).ToArray();
            if (scenarios.Select(s => s.OperationId).Distinct().Count() != scenarios.Length)
                throw new InvalidDataException("Duplicate caller scenarios cannot be merged.");
            var proposals = entries.SelectMany(d => d.Proposals)
                .GroupBy(p => JsonSerializer.Serialize(p with { Evidence = [] }, ToolJson.Options), StringComparer.Ordinal)
                .Select(g => g.First() with { Evidence = g.SelectMany(p => p.Evidence).DistinctBy(c => (c.Path, c.StartLine, c.EndLine)).ToArray() }).ToArray();
            var missing = operations.Where(o => o.CheckId == check.Id)
                .Any(o => coverage.Single(c => c.OperationId == o.Id).Status is not ("inspected" or "not-applicable"));
            var incomplete = missing || entries.Length == 0 || entries.Any(d => d.Outcome == "inconclusive") ||
                discovery.Any(d => d.CheckId == check.Id && d.Disposition == "inconclusive");
            return new ErgonomicsDecision(check.Id, proposals.Length > 0 ? "recommendations" : incomplete ? "inconclusive" : "no-improvement",
                entries.Length == 0 ? "The assigned caller review did not produce accepted output." :
                    string.Join(" ", entries.Select(d => d.Reason)) + (incomplete ? " Some assigned work remains unresolved; retained findings have limited scope." : ""),
                proposals, entries.SelectMany(d => d.Evidence).DistinctBy(c => (c.Path, c.StartLine, c.EndLine)).ToArray(),
                scenarios.Length > 0 ? scenarios :
                    [new("Assess the assigned caller operations.", "Caller analysis is unavailable.", "No alternative was evaluated.",
                        "The assigned review did not produce accepted output.", [], operations.First(o => o.CheckId == check.Id).Id)]);
        }).ToArray();
        return new(1, input.ReviewId, decisions, coverage, additional, discovery);
    }

    internal static ErgonomicsValidation ValidateTask(ErgonomicsInput input, ErgonomicsTask task,
        ErgonomicsReview review, string workspace, ErgonomicsReview? discovery)
    {
        var expected = Plan(input, task.Kind, workspace, discovery).SingleOrDefault(t => t.TaskId == task.TaskId);
        if (expected is null || JsonSerializer.Serialize(expected, ToolJson.Options) != JsonSerializer.Serialize(task, ToolJson.Options))
            throw new InvalidDataException("Task definition changed or belongs to a different plan.");
        var composite = Merge(input, discovery is null ? [review] : [discovery, review]);
        // Validate the submitted decision itself, including alternatives and scenario
        // evidence. Only the global no-improvement coverage condition is deferred until
        // all tasks have been merged; every assigned operation must still be complete.
        composite = composite with { Decisions = composite.Decisions.Select(d =>
            review.Decisions.SingleOrDefault(own => own.CheckId == d.CheckId) ?? d).ToArray() };
        var result = ErgonomicsReviewPipeline.Validate(input, composite, workspace);
        var errors = result.Errors.Where(e => !e.StartsWith("No-improvement requires inspected coverage of every operation;", StringComparison.Ordinal)).ToList();
        foreach (var decision in review.Decisions.Where(d => d.Outcome == "no-improvement"))
            if (task.Operations.Where(o => o.CheckId == decision.CheckId).Any(o =>
                !review.Coverage.Any(c => c.OperationId == o.Id && c.Status is "inspected" or "not-applicable")))
                errors.Add("No-improvement requires completed coverage of every assigned operation.");
        return result with { Valid = errors.Count == 0, Errors = errors };
    }
}

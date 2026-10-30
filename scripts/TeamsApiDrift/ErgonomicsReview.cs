using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record ErgonomicsSurface(string Id, string Role, string Description,
    IReadOnlyList<string> Symbols, IReadOnlyList<string> PathHints);

public sealed record ErgonomicsOperation(string Id, string CheckId, string Description,
    IReadOnlyList<ErgonomicsSurface> Surfaces, bool ResponseChain = false);

public sealed record ErgonomicsCatalog(int SchemaVersion, IReadOnlyList<BehaviorCheck> Checks,
    IReadOnlyList<ErgonomicsOperation> Operations);

public sealed record ErgonomicsInput(int SchemaVersion, string ReviewId, ErgonomicsCatalog Catalog,
    IReadOnlyList<BehaviorSource> Sources, IReadOnlyList<ReviewArtifact> Files, ErgonomicsNavigation Navigation);

public sealed record ErgonomicsCompatibility(string Source, string Binary, string Behavior, string Serialization,
    string BaselineRationale);

public sealed record ErgonomicsProposal(string Title, string AffectedApi, string Project, string Scope,
    string Problem, string Change, string Before, string After, string Benefit, string Inspiration,
    ErgonomicsCompatibility Compatibility, string ImplementationTests, IReadOnlyList<ReviewCitation> Evidence,
    string? SharedApiRationale = null);

public sealed record ErgonomicsAlternative(string Kind, string CurrentCallerCode, string ProposedCallerCode,
    ErgonomicsCompatibility Compatibility, string BenefitAssessment);

public sealed record ErgonomicsScenario(string CallerTask, string CurrentExperience, string ImprovementConsidered,
    string Assessment, IReadOnlyList<ReviewCitation> Evidence, string OperationId,
    ErgonomicsAlternative? Alternative = null);

public sealed record ErgonomicsSurfaceEvidence(string SurfaceId, IReadOnlyList<ReviewCitation> Evidence,
    string Resolution, IReadOnlyList<string> ResolvedSymbols, string Status = "inspected");

public sealed record ErgonomicsResponseStep(string Stage, string Description, IReadOnlyList<ReviewCitation> Evidence,
    string Status = "inspected");

public sealed record ErgonomicsOperationCoverage(string OperationId, string Status, string Reason,
    IReadOnlyList<ErgonomicsSurfaceEvidence> Surfaces, IReadOnlyList<ErgonomicsResponseStep> ResponseChain);

public sealed record ErgonomicsDecision(string CheckId, string Outcome, string Reason,
    IReadOnlyList<ErgonomicsProposal> Proposals, IReadOnlyList<ReviewCitation> Evidence,
    IReadOnlyList<ErgonomicsScenario> Scenarios);

public sealed record ErgonomicsDiscoveryAssessment(string DiscoveryId, string CheckId, string Disposition,
    IReadOnlyList<string> OperationIds, string Reason, IReadOnlyList<ReviewCitation> Evidence);

public sealed record ErgonomicsReview(int SchemaVersion, string ReviewId, IReadOnlyList<ErgonomicsDecision> Decisions,
    IReadOnlyList<ErgonomicsOperationCoverage> Coverage, IReadOnlyList<ErgonomicsOperation> AdditionalOperations,
    IReadOnlyList<ErgonomicsDiscoveryAssessment> Discovery);

public sealed record ErgonomicsValidation(int SchemaVersion, bool Valid, bool HasRecommendations,
    IReadOnlyList<string> Errors, IReadOnlyList<ErgonomicsDecision> Decisions,
    IReadOnlyList<ErgonomicsOperationCoverage> Coverage, IReadOnlyList<ErgonomicsOperation> AdditionalOperations,
    IReadOnlyList<ErgonomicsDiscoveryAssessment> Discovery);

public static class ErgonomicsReviewPipeline
{
    public const int SchemaVersion = 1;
    private static readonly string[] Roles = ["agents", "teams-baseline", "teams-candidate"];
    private static readonly string[] ResponseStages = ["response-construction", "upstream-handler", "local-handler", "dispatcher"];
    private static string Identity(ErgonomicsInput input) =>
        ImpactReviewPipeline.Hash(JsonSerializer.Serialize(input with { ReviewId = "" }, ToolJson.Options));

    public static async Task<ErgonomicsInput> PrepareAsync(string repositoryRoot, string workspace,
        ErgonomicsCatalog catalog, string to)
    {
        ValidateCatalog(catalog);
        var agents = BehaviorReviewPipeline.CaptureAgents(repositoryRoot, workspace);
        var declared = DeclaredVersion(agents.Files);
        var baseline = await BehaviorReviewPipeline.DownloadAsync(declared, "teams-baseline").ConfigureAwait(false);
        // Preserve both reference roles without downloading the same release twice.
        var candidate = VersionComparer.VersionRelease.Equals(NuGetVersion.Parse(declared), NuGetVersion.Parse(to))
            ? (baseline.Source with { Role = "teams-candidate", Version = to }, baseline.Files)
            : await BehaviorReviewPipeline.DownloadAsync(to, "teams-candidate").ConfigureAwait(false);

        return Prepare(workspace, catalog, [agents, baseline, candidate]);
    }

    internal static string DeclaredVersion(IReadOnlyDictionary<string, string> files)
    {
        using var reader = new StringReader(files["Directory.Packages.props"]);

        return VersionResolver.Resolve(reader);
    }

    internal static ErgonomicsInput Prepare(string workspace, ErgonomicsCatalog catalog,
        IReadOnlyList<(BehaviorSource Source, Dictionary<string, string> Files)> sources)
    {
        ValidateCatalog(catalog);

        if (sources.Count != Roles.Length || !sources.Select(s => s.Source.Role).Order().SequenceEqual(Roles.Order()))
        {
            throw new InvalidDataException("Agents, declared Teams baseline, and Teams candidate sources are required.");
        }

        var declared = DeclaredVersion(sources.Single(s => s.Source.Role == "agents").Files);

        if (sources.Single(s => s.Source.Role == "teams-baseline").Source.Version != declared)
        {
            throw new InvalidDataException("Ergonomics baseline must match the checkout's declared dependency.");
        }

        var files = BehaviorReviewPipeline.WriteSnapshot(workspace, sources);
        var index = ErgonomicsSourceIndex.Create(workspace, files);
        var input = new ErgonomicsInput(SchemaVersion, "", catalog, sources.Select(s => s.Source).ToArray(), files,
            index.Navigate(catalog.Operations));
        input = input with { ReviewId = Identity(input) };
        ToolJson.Write(Path.Combine(workspace, "ergonomics-input.json"), input);

        return input;
    }

    private static void ValidateCatalog(ErgonomicsCatalog catalog)
    {
        if (catalog.SchemaVersion != SchemaVersion || catalog.Checks is null || catalog.Checks.Count == 0 ||
            catalog.Checks.Any(c => string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Description)) ||
            catalog.Checks.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Checks.Count)
        {
            throw new InvalidDataException("Invalid ergonomics catalog or duplicate check IDs.");
        }

        if (catalog.Operations is null || catalog.Operations.Count == 0 ||
            catalog.Operations.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Operations.Count ||
            catalog.Checks.Any(c => !catalog.Operations.Any(o => o.CheckId == c.Id)))
        {
            throw new InvalidDataException("Every ergonomics check requires cataloged operations with unique IDs.");
        }

        foreach (var operation in catalog.Operations)
        {
            if (string.IsNullOrWhiteSpace(operation.Id) || string.IsNullOrWhiteSpace(operation.Description) ||
                !catalog.Checks.Any(c => c.Id == operation.CheckId) || operation.Surfaces is null || operation.Surfaces.Count == 0 ||
                operation.Surfaces.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != operation.Surfaces.Count ||
                operation.Surfaces.Any(s => string.IsNullOrWhiteSpace(s.Id) || !Roles.Contains(s.Role, StringComparer.Ordinal) ||
                    string.IsNullOrWhiteSpace(s.Description) || s.Symbols is null || s.PathHints is null ||
                    (s.Symbols.Count == 0 && s.PathHints.Count == 0) || s.Symbols.Any(string.IsNullOrWhiteSpace) ||
                    s.PathHints.Any(p => !p.StartsWith(s.Role + "/", StringComparison.Ordinal) ||
                        p.Contains('\\') || p.Split('/').Any(part => part is "" or "." or ".."))) ||
                !operation.Surfaces.Any(s => s.Role == "agents") ||
                (operation.ResponseChain && ResponseStages.Any(stage =>
                    !operation.Surfaces.Any(s => s.Id == stage && s.Role ==
                        (stage is "response-construction" or "upstream-handler" ? "teams-baseline" : "agents")))))
            {
                throw new InvalidDataException($"Invalid operation comparison surfaces: {operation.Id}.");
            }
        }
    }

    public static ErgonomicsReview Parse(string json) => JsonSerializer.Deserialize<ErgonomicsReview>(json,
        new JsonSerializerOptions(ToolJson.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Empty ergonomics review.");

    public static ErgonomicsValidation Validate(ErgonomicsInput input, ErgonomicsReview review, string workspace)
    {
        ValidateCatalog(input.Catalog);
        var errors = new List<string>();
        if (review.AdditionalOperations is null || review.Discovery is null)
            errors.Add("Additional operations and discovery reconciliation arrays are required.");

        if (input.SchemaVersion != SchemaVersion || review.SchemaVersion != SchemaVersion || input.ReviewId != Identity(input) ||
            review.ReviewId != input.ReviewId)
        {
            errors.Add("Review identity or schema mismatch.");
        }

        if (input.Sources.Count != Roles.Length || !input.Sources.Select(s => s.Role).Order().SequenceEqual(Roles.Order()) ||
            input.Files.Any(f => !Roles.Any(role => f.Path.StartsWith(role + "/", StringComparison.Ordinal))))
        {
            errors.Add("Invalid ergonomics source roles.");
        }

        var snapshotInput = ToolJson.Read<ErgonomicsInput>(Path.Combine(workspace, "ergonomics-input.json"));

        if (JsonSerializer.Serialize(snapshotInput, ToolJson.Options) != JsonSerializer.Serialize(input, ToolJson.Options))
        {
            errors.Add("Snapshot input changed.");
        }

        var inventory = input.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);

        foreach (var file in input.Files)
        {
            var path = ImpactReviewPipeline.Resolve(workspace, file.Path);
            if (!File.Exists(path) || ImpactReviewPipeline.Hash(File.ReadAllText(path)) != file.ContentHash)
            {
                errors.Add($"Source changed: {file.Path}.");
            }
        }

        const string props = "agents/Directory.Packages.props";

        if (!inventory.ContainsKey(props) || !File.Exists(Path.Combine(workspace, props)) ||
            input.Sources.SingleOrDefault(s => s.Role == "teams-baseline")?.Version !=
            DeclaredVersion(new Dictionary<string, string> { ["Directory.Packages.props"] = File.ReadAllText(Path.Combine(workspace, props)) }))
        {
            errors.Add("Baseline does not match the declared dependency.");
        }

        IReadOnlyList<ReviewCitation> Citations(IReadOnlyList<ReviewCitation>? evidence)
        {
            var resolved = new List<ReviewCitation>();

            foreach (var citation in evidence ?? [])
            {
                if (!inventory.ContainsKey(citation.Path))
                {
                    errors.Add($"Unknown citation: {citation.Path}.");
                    continue;
                }

                var path = ImpactReviewPipeline.Resolve(workspace, citation.Path);

                if (!File.Exists(path))
                {
                    errors.Add($"Missing citation source: {citation.Path}.");
                    continue;
                }

                var lines = File.ReadAllText(path).Split('\n');

                if (citation.StartLine is not int start || citation.EndLine is not int end || start < 1 || end < start || end > lines.Length)
                {
                    errors.Add($"Invalid citation range: {citation.Path}.");
                    continue;
                }

                var quote = string.Join("\n", lines[(start - 1)..end]);

                if (string.IsNullOrWhiteSpace(quote) || (citation.Quote is not null && citation.Quote != quote))
                {
                    errors.Add($"Invalid citation quote: {citation.Path}.");
                    continue;
                }

                resolved.Add(citation with { Quote = quote });
            }
            return resolved;
        }

        var additional = review.AdditionalOperations ?? [];
        try
        {
            ValidateCatalog(input.Catalog with { Operations = [.. input.Catalog.Operations, .. additional] });
        }
        catch (InvalidDataException exception)
        {
            return new(SchemaVersion, false, false, [.. errors, exception.Message], [], [], [], []);
        }
        var allOperations = input.Catalog.Operations.Concat(additional).ToArray();
        var operations = allOperations.ToDictionary(o => o.Id, StringComparer.Ordinal);
        var sourceIndex = ErgonomicsSourceIndex.Create(workspace, input.Files);
        var coverage = new List<ErgonomicsOperationCoverage>();
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in review.Coverage ?? [])
        {
            if (!operations.TryGetValue(item.OperationId, out var operation) || !covered.Add(item.OperationId))
            {
                errors.Add($"Unknown or duplicate operation coverage: {item.OperationId}.");
                continue;
            }

            if (item.Status is not ("inspected" or "uninspected" or "inconclusive" or "not-applicable") ||
                string.IsNullOrWhiteSpace(item.Reason) || item.Surfaces is null || item.ResponseChain is null)
            {
                errors.Add($"Incomplete operation coverage: {item.OperationId}.");
            }

            var surfaces = new List<ErgonomicsSurfaceEvidence>();
            var surfaceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var surface in item.Surfaces ?? [])
            {
                var expected = operation.Surfaces.SingleOrDefault(s => s.Id == surface.SurfaceId);
                var evidence = Citations(surface.Evidence);
                if (expected is null || !surfaceIds.Add(surface.SurfaceId) || evidence.Count == 0 ||
                    string.IsNullOrWhiteSpace(surface.Resolution) || surface.Status is not ("inspected" or "not-applicable") ||
                    !Cites(evidence, expected.Role) ||
                    (surface.Status == "inspected" && !sourceIndex.Binds(expected, surface, evidence)) ||
                    (surface.Status == "not-applicable" && !sourceIndex.CanEstablishAbsence(expected)))
                {
                    errors.Add($"Missing or invalid comparison surface: {item.OperationId}/{surface.SurfaceId}.");
                }
                surfaces.Add(surface with { Evidence = evidence });
            }

            if (item.Status == "inspected" && !surfaceIds.SetEquals(operation.Surfaces.Select(s => s.Id)))
            {
                errors.Add($"Inspected operation requires every comparison surface: {item.OperationId}.");
            }

            if (item.Status == "not-applicable" && !surfaces.Any(s => s.Status == "not-applicable"))
            {
                errors.Add($"Not-applicable operation requires an evidence-backed absent surface: {item.OperationId}.");
            }

            var steps = new List<ErgonomicsResponseStep>();
            var stages = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in item.ResponseChain ?? [])
            {
                var evidence = Citations(step.Evidence);
                var expected = operation.Surfaces.SingleOrDefault(s => s.Id == step.Stage);
                if (!operation.ResponseChain || !ResponseStages.Contains(step.Stage, StringComparer.Ordinal) ||
                    !stages.Add(step.Stage) || string.IsNullOrWhiteSpace(step.Description) || expected is null ||
                    step.Status is not ("inspected" or "not-applicable") || !Cites(evidence, expected.Role) ||
                    !surfaces.Any(s => s.SurfaceId == step.Stage && s.Status == step.Status &&
                        (step.Status == "not-applicable" || sourceIndex.Binds(expected, s, evidence))))
                {
                    errors.Add($"Missing or invalid response-chain stage: {item.OperationId}/{step.Stage}.");
                }
                steps.Add(step with { Evidence = evidence });
            }

            if (operation.ResponseChain && item.Status == "inspected" && !stages.SetEquals(ResponseStages))
            {
                errors.Add($"Inspected response operation requires the complete response chain: {item.OperationId}.");
            }

            coverage.Add(item with { Surfaces = surfaces, ResponseChain = steps });
        }

        if (!covered.SetEquals(operations.Keys))
        {
            errors.Add("Every cataloged or additional operation requires exactly one explicit coverage result.");
        }

        var discovery = new List<ErgonomicsDiscoveryAssessment>();
        var discoveredIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assessment in review.Discovery ?? [])
        {
            var found = input.Navigation.DiscoveredOperations.SingleOrDefault(d => d.Id == assessment.DiscoveryId);
            var evidence = Citations(assessment.Evidence);
            var mapped = assessment.OperationIds ?? [];
            if (found is null || !discoveredIds.Add(assessment.DiscoveryId) ||
                !input.Catalog.Checks.Any(c => c.Id == assessment.CheckId) || string.IsNullOrWhiteSpace(assessment.Reason) ||
                assessment.Disposition is not ("cataloged" or "additional" or "not-applicable" or "inconclusive") ||
                mapped.Distinct(StringComparer.Ordinal).Count() != mapped.Count || mapped.Any(id => !operations.ContainsKey(id)) ||
                (assessment.Disposition is "cataloged" or "additional" && mapped.Count == 0) ||
                (assessment.Disposition == "cataloged" && mapped.Any(id => !input.Catalog.Operations.Any(o => o.Id == id))) ||
                (assessment.Disposition == "additional" && !mapped.Any(id => additional.Any(o => o.Id == id))) ||
                (assessment.Disposition is "not-applicable" or "inconclusive" && mapped.Count != 0) ||
                found.Evidence.GroupBy(c => c.Path.Split('/')[0]).Any(role =>
                    !role.Any(declaration => evidence.Any(c => ErgonomicsSourceIndex.Overlaps(c, declaration)))))
            {
                errors.Add($"Incomplete discovery reconciliation or missing version evidence: {assessment.DiscoveryId}.");
            }
            discovery.Add(assessment with { Evidence = evidence });
        }
        if (!discoveredIds.SetEquals(input.Navigation.DiscoveredOperations.Select(d => d.Id)))
            errors.Add("Every discovered registration requires an explicit catalog/additional/not-applicable/inconclusive reconciliation.");
        if (additional.Any(o => !discovery.Any(d => d.Disposition == "additional" && d.OperationIds.Contains(o.Id, StringComparer.Ordinal))))
            errors.Add("Additional operations must reconcile a discovered registration.");
        if (coverage.Any(c => c.Status == "not-applicable" && discovery.Any(d => d.OperationIds.Contains(c.OperationId, StringComparer.Ordinal))))
            errors.Add("An operation mapped to a present registration cannot be dismissed as not-applicable; inspect its current API roles.");

        var required = input.Catalog.Checks.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var decisions = new List<ErgonomicsDecision>();

        foreach (var decision in review.Decisions ?? [])
        {
            if (!required.Contains(decision.CheckId) || !seen.Add(decision.CheckId))
            {
                errors.Add($"Unknown or duplicate check: {decision.CheckId}.");
            }

            if (decision.Outcome is not ("recommendations" or "no-improvement" or "inconclusive") ||
                string.IsNullOrWhiteSpace(decision.Reason) || decision.Proposals is null || decision.Evidence is null ||
                (decision.Outcome == "recommendations") != (decision.Proposals?.Count > 0))
            {
                errors.Add($"Invalid decision: {decision.CheckId}.");
            }

            var evidence = Citations(decision.Evidence);

            if (decision.Outcome == "no-improvement" && !Cites(evidence, "agents"))
            {
                errors.Add($"No-improvement conclusion requires Agents evidence: {decision.CheckId}.");
            }

            if (decision.Outcome == "no-improvement" &&
                (allOperations.Where(o => o.CheckId == decision.CheckId)
                    .Any(o => !coverage.Any(c => c.OperationId == o.Id && c.Status is "inspected" or "not-applicable")) ||
                 discovery.Any(d => d.CheckId == decision.CheckId && d.Disposition == "inconclusive")))
            {
                errors.Add($"No-improvement requires inspected coverage of every operation; use inconclusive for missing comparisons: {decision.CheckId}.");
            }

            var scenarios = new List<ErgonomicsScenario>();

            if (decision.Scenarios is null || decision.Scenarios.Count == 0)
            {
                errors.Add($"Every check requires concrete caller scenarios and considered improvements: {decision.CheckId}.");
            }

            foreach (var scenario in decision.Scenarios ?? [])
            {
                if (!operations.TryGetValue(scenario.OperationId, out var operation) || operation.CheckId != decision.CheckId)
                {
                    errors.Add($"Scenario references an unknown operation or different check: {scenario.OperationId}.");
                }
                if (new[] { scenario.CallerTask, scenario.CurrentExperience, scenario.ImprovementConsidered,
                    scenario.Assessment }.Any(string.IsNullOrWhiteSpace) || scenario.Evidence is null)
                {
                    errors.Add($"Incomplete caller scenario: {decision.CheckId}.");
                }

                var scenarioEvidence = Citations(scenario.Evidence);

                if (decision.Outcome != "inconclusive" && !Cites(scenarioEvidence, "agents"))
                {
                    errors.Add($"Caller scenario requires Agents evidence: {decision.CheckId}.");
                }

                if (decision.Outcome == "no-improvement" &&
                    (scenario.Alternative is not { } alternative ||
                     alternative.Kind is not ("additive-api" or "documentation" or "existing-path") ||
                     new[] { alternative.CurrentCallerCode, alternative.ProposedCallerCode, alternative.BenefitAssessment }
                         .Any(string.IsNullOrWhiteSpace) || !CompleteCompatibility(alternative.Compatibility)))
                {
                    errors.Add($"No-improvement requires a compatible alternative, current/proposed caller code, compatibility reasoning and benefit assessment: {scenario.OperationId}.");
                }

                scenarios.Add(scenario with { Evidence = scenarioEvidence });
            }

            foreach (var operation in allOperations.Where(o => o.CheckId == decision.CheckId))
            {
                if (coverage.Any(c => c.OperationId == operation.Id && c.Status == "inspected") &&
                    !scenarios.Any(s => s.OperationId == operation.Id))
                {
                    errors.Add($"Inspected operation requires its own caller assessment: {operation.Id}.");
                }
            }

            var proposals = new List<ErgonomicsProposal>();

            foreach (var proposal in decision.Proposals ?? [])
            {
                if (new[] { proposal.Title, proposal.AffectedApi, proposal.Project, proposal.Problem, proposal.Change,
                    proposal.Before, proposal.After, proposal.Benefit, proposal.ImplementationTests }.Any(string.IsNullOrWhiteSpace) ||
                    proposal.Scope is not ("msteams" or "shared") || proposal.Inspiration is not ("agents" or "baseline" or "candidate"))
                {
                    errors.Add($"Incomplete proposal: {proposal.Title}.");
                }

                if (!CompleteCompatibility(proposal.Compatibility))
                {
                    errors.Add($"Proposal requires source, binary, behavioral, serialization, and baseline compatibility reasoning: {proposal.Title}.");
                }

                if (proposal.Scope == "shared" && string.IsNullOrWhiteSpace(proposal.SharedApiRationale))
                {
                    errors.Add($"Shared API proposal requires an extension-boundary rationale: {proposal.Title}.");
                }

                var proposalEvidence = Citations(proposal.Evidence);

                if (!Cites(proposalEvidence, "agents") ||
                    (proposal.Inspiration == "baseline" && !Cites(proposalEvidence, "teams-baseline")) ||
                    (proposal.Inspiration == "candidate" && (!Cites(proposalEvidence, "teams-candidate") || !Cites(proposalEvidence, "teams-baseline"))))
                {
                    errors.Add($"Missing Agents or version-specific Teams evidence: {proposal.Title}.");
                }

                proposals.Add(proposal with { Evidence = proposalEvidence });
            }
            decisions.Add(decision with { Evidence = evidence, Proposals = proposals, Scenarios = scenarios });
        }

        if (!required.SetEquals(seen))
        {
            errors.Add("Every ergonomics catalog check must have exactly one decision.");
        }

        return new(SchemaVersion, errors.Count == 0, decisions.Any(d => d.Proposals.Count > 0), errors, decisions, coverage, additional, discovery);
    }

    private static bool CompleteCompatibility(ErgonomicsCompatibility? compatibility) => compatibility is not null &&
        !new[] { compatibility.Source, compatibility.Binary, compatibility.Behavior, compatibility.Serialization,
            compatibility.BaselineRationale }.Any(string.IsNullOrWhiteSpace);

    private static bool Cites(IReadOnlyList<ReviewCitation> evidence, string role) =>
        evidence.Any(c => c.Path.StartsWith(role + "/", StringComparison.Ordinal));

    public static string Render(ErgonomicsInput input, ErgonomicsValidation validation)
    {
        var text = new StringBuilder("# MSTeams API ergonomics review\n\n");
        text.AppendLine("Recommendations are advisory, unimplemented designs. Caller examples have not been compiled. " +
            "Evidence validation checks source integrity and report structure; compatibility reasoning still requires implementation tests.\n");
        text.AppendLine($"Evidence validation: **{(validation.Valid ? "passed" : "FAILED")}**. Review: `{input.ReviewId}`.\n");
        text.AppendLine("The declared Teams dependency is the primary reference; the candidate provides additional inspiration.\n");

        foreach (var source in input.Sources)
        {
            text.AppendLine($"- {source.Role}: `{source.Version}` at `{source.Commit}`");
        }

        foreach (var error in validation.Errors)
        {
            text.AppendLine($"\nValidation error: {error}");
        }
        // Invalid proposals remain diagnostic JSON, never a readable recommendation report.
        if (!validation.Valid)
        {
            return text.ToString();
        }

        if (!validation.HasRecommendations)
        {
            text.AppendLine("\nNo compatible API ergonomics improvements were proposed in this review.");
        }

        var inspected = validation.Coverage.Count(c => c.Status == "inspected");
        var operationCount = input.Catalog.Operations.Count + validation.AdditionalOperations.Count;
        text.AppendLine($"\n## Operation coverage\n\n{inspected} of {operationCount} required operations inspected; " +
            $"{validation.Coverage.Count(c => c.Status == "not-applicable")} not applicable with source evidence. " +
            "Uninspected or inconclusive operations are review limitations, not evidence that the API needs no improvement. " +
            "This inventory covers seeded and discovered tasks, not every public member or overload.\n");
        text.AppendLine("| Operation | Status | Assessment / limitation |\n|---|---|---|");
        foreach (var item in validation.Coverage)
        {
            text.AppendLine($"| {TableText(item.OperationId)} | {item.Status} | {TableText(item.Reason)} |");
        }

        text.AppendLine($"\n## Version-specific discovery\n\n{input.Navigation.DiscoveredOperations.Count} registration candidates reconciled. " +
            "Candidate additions/removals are relative to the declared baseline; they are not inferred historical changes to Agents.\n");
        foreach (var limitation in input.Navigation.Limitations) text.AppendLine($"- {limitation}");
        text.AppendLine("\n| Registration | Availability | Disposition | Reason |\n|---|---|---|---|");
        foreach (var assessment in validation.Discovery)
        {
            var found = input.Navigation.DiscoveredOperations.Single(d => d.Id == assessment.DiscoveryId);
            if (found.Availability is "candidate-added" or "candidate-removed" ||
                assessment.Disposition is "additional" or "inconclusive" or "not-applicable")
                text.AppendLine($"| {TableText(found.Symbol)} | {found.Availability} | {assessment.Disposition} | {TableText(assessment.Reason)} |");
        }

        text.AppendLine("\n## Review assessments\n\n| Area | Outcome | Reason |\n|---|---|---|");
        foreach (var decision in validation.Decisions)
        {
            text.AppendLine($"| {TableText(decision.CheckId)} | {decision.Outcome} | {TableText(decision.Reason)} |");
        }

        foreach (var item in validation.Coverage.Where(c => c.ResponseChain.Count > 0))
        {
            text.AppendLine($"\n### Response chain: {item.OperationId} ({item.Status})\n");
            foreach (var step in item.ResponseChain)
            {
                text.AppendLine($"**{step.Stage} ({step.Status}):** {step.Description}");
                Sources(step.Evidence);
                text.AppendLine();
            }
        }

        foreach (var decision in validation.Decisions)
        {
            if (decision.Outcome == "inconclusive")
            {
                text.AppendLine($"\n## Review limitation: {decision.CheckId}\n\n{decision.Reason}");
                Sources(decision.Evidence);
            }

            foreach (var proposal in decision.Proposals)
            {
                text.AppendLine($"\n## {proposal.Title}\n\n{proposal.Problem}\n\n**Proposed change:** {proposal.Change}");
                text.AppendLine($"\n**Benefit:** {proposal.Benefit}\n\n**API / project:** `{proposal.AffectedApi}` / `{proposal.Project}`");
                text.AppendLine($"\n**Inspiration:** {proposal.Inspiration}. Review area: {decision.CheckId}.");
                Example("Current caller code", proposal.Before);
                Example("Proposed caller code (unimplemented)", proposal.After);
                text.AppendLine($"\n**Source compatibility:** {proposal.Compatibility.Source}");
                text.AppendLine($"\n**Binary compatibility:** {proposal.Compatibility.Binary}");
                text.AppendLine($"\n**Behavior compatibility:** {proposal.Compatibility.Behavior}");
                text.AppendLine($"\n**Serialization compatibility:** {proposal.Compatibility.Serialization}");
                text.AppendLine($"\n**Works with the declared dependency:** {proposal.Compatibility.BaselineRationale}");

                if (proposal.Scope == "shared")
                {
                    text.AppendLine($"\n**Why this needs a shared API:** {proposal.SharedApiRationale}");
                }

                text.AppendLine($"\n**Implementation tests:** {proposal.ImplementationTests}");
                Sources(proposal.Evidence);
            }
        }
        return text.ToString();

        void Sources(IReadOnlyList<ReviewCitation> citations) => text.AppendLine("\n**Sources:** " +
            string.Join(" · ", citations.DistinctBy(c => (c.Path, c.StartLine, c.EndLine)).Select(c => RenderSource(input, c))));
        void Example(string title, string code)
        {
            var fence = new string('`', Math.Max(3, Regex.Matches(code, "`+").Select(m => m.Length + 1).DefaultIfEmpty(3).Max()));
            text.AppendLine($"\n**{title}:**\n\n{fence}csharp\n{code}\n{fence}");
        }
    }

    private static string TableText(string value) => value.Replace("|", "&#124;").Replace("\r", "").Replace("\n", "<br>");

    private static string RenderSource(ErgonomicsInput input, ReviewCitation citation)
    {
        var separator = citation.Path.IndexOf('/');
        var role = citation.Path[..separator];
        var path = citation.Path[(separator + 1)..];
        var source = input.Sources.Single(s => s.Role == role);
        var label = $"{role}: {Path.GetFileName(path)} ({citation.StartLine}–{citation.EndLine})";

        if (Regex.IsMatch(source.Repository, @"\Ahttps://github\.com/[\w.-]+/[\w.-]+\z") &&
            Regex.IsMatch(source.Commit, @"\A[0-9a-fA-F]{40}\z"))
        {
            return $"[{label}]({source.Repository}/blob/{source.Commit}/{string.Join("/", path.Split('/').Select(Uri.EscapeDataString))}#L{citation.StartLine}-L{citation.EndLine})";
        }

        return $"{role} snapshot: `{path}:{citation.StartLine}-{citation.EndLine}`";
    }
}

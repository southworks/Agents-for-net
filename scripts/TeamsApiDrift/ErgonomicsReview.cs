using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record ErgonomicsCatalog(int SchemaVersion, IReadOnlyList<BehaviorCheck> Checks);

public sealed record ErgonomicsInput(int SchemaVersion, string ReviewId, ErgonomicsCatalog Catalog,
    IReadOnlyList<BehaviorSource> Sources, IReadOnlyList<ReviewArtifact> Files);

public sealed record ErgonomicsCompatibility(string Source, string Binary, string Behavior, string Serialization,
    string BaselineRationale);

public sealed record ErgonomicsProposal(string Title, string AffectedApi, string Project, string Scope,
    string Problem, string Change, string Before, string After, string Benefit, string Inspiration,
    ErgonomicsCompatibility Compatibility, string ImplementationTests, IReadOnlyList<ReviewCitation> Evidence,
    string? SharedApiRationale = null);

public sealed record ErgonomicsScenario(string CallerTask, string CurrentExperience, string ImprovementConsidered,
    string Assessment, IReadOnlyList<ReviewCitation> Evidence);

public sealed record ErgonomicsDecision(string CheckId, string Outcome, string Reason,
    IReadOnlyList<ErgonomicsProposal> Proposals, IReadOnlyList<ReviewCitation> Evidence,
    IReadOnlyList<ErgonomicsScenario> Scenarios);

public sealed record ErgonomicsReview(int SchemaVersion, string ReviewId, IReadOnlyList<ErgonomicsDecision> Decisions);

public sealed record ErgonomicsValidation(int SchemaVersion, bool Valid, bool HasRecommendations,
    IReadOnlyList<string> Errors, IReadOnlyList<ErgonomicsDecision> Decisions);

public static class ErgonomicsReviewPipeline
{
    private static readonly string[] Roles = ["agents", "teams-baseline", "teams-candidate"];
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
        var input = new ErgonomicsInput(1, "", catalog, sources.Select(s => s.Source).ToArray(), files);
        input = input with { ReviewId = Identity(input) };
        ToolJson.Write(Path.Combine(workspace, "ergonomics-input.json"), input);

        return input;
    }

    private static void ValidateCatalog(ErgonomicsCatalog catalog)
    {
        if (catalog.SchemaVersion != 1 || catalog.Checks is null || catalog.Checks.Count == 0 ||
            catalog.Checks.Any(c => string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Description)) ||
            catalog.Checks.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Checks.Count)
        {
            throw new InvalidDataException("Invalid ergonomics catalog or duplicate check IDs.");
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

        if (input.SchemaVersion != 1 || review.SchemaVersion != 1 || input.ReviewId != Identity(input) ||
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

            var scenarios = new List<ErgonomicsScenario>();

            if (decision.Scenarios is null || decision.Scenarios.Count == 0)
            {
                errors.Add($"Every check requires concrete caller scenarios and considered improvements: {decision.CheckId}.");
            }

            foreach (var scenario in decision.Scenarios ?? [])
            {
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

                scenarios.Add(scenario with { Evidence = scenarioEvidence });
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

                if (proposal.Compatibility is not { } compatibility ||
                    new[] { compatibility.Source, compatibility.Binary, compatibility.Behavior, compatibility.Serialization,
                        compatibility.BaselineRationale }.Any(string.IsNullOrWhiteSpace))
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

        return new(1, errors.Count == 0, decisions.Any(d => d.Proposals.Count > 0), errors, decisions);
    }

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

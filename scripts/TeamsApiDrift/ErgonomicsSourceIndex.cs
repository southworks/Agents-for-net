using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml.Linq;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record ErgonomicsSymbolLocation(string Symbol, ReviewCitation Evidence, string? Reference = null);

public sealed record ErgonomicsIndexedCitation(string Id, ReviewCitation Citation);

public sealed record ErgonomicsSurfaceLocation(string OperationId, string SurfaceId,
    IReadOnlyList<ErgonomicsSymbolLocation> Candidates);

public sealed record ErgonomicsDiscoveredOperation(string Id, string Symbol, string Availability,
    IReadOnlyList<ReviewCitation> Evidence, IReadOnlyList<string>? EvidenceReferences = null);

public sealed record ErgonomicsNavigation(IReadOnlyList<ErgonomicsSurfaceLocation> Surfaces,
    IReadOnlyList<ErgonomicsDiscoveredOperation> DiscoveredOperations, IReadOnlyList<string> Limitations,
    IReadOnlyList<ErgonomicsIndexedCitation> IndexedCitations);

// A syntax-only navigation index. It does not compile snapshots or prove API/service availability.
internal sealed class ErgonomicsSourceIndex
{
    private sealed record Declaration(string Role, string Symbol, bool Registration, IReadOnlyList<string> Containers, ReviewCitation Evidence);
    private readonly List<Declaration> _declarations = [];
    private readonly List<string> _limitations = [];
    private readonly HashSet<string> _unreliableRoles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _publicTypes = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<ReviewArtifact> _files;

    private ErgonomicsSourceIndex(IReadOnlyList<ReviewArtifact> files) => _files = files;

    internal static ErgonomicsSourceIndex Create(string workspace, IReadOnlyList<ReviewArtifact> files)
    {
        var index = new ErgonomicsSourceIndex(files);
        var diagnostics = new HashSet<string>(StringComparer.Ordinal);
        var conditionalRoles = new HashSet<string>(StringComparer.Ordinal);
        var projectRoots = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var project in files.Where(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            var role = project.Path.Split('/')[0];
            var expected = role == "agents" ? "Microsoft.Agents.Extensions.MSTeams" : "Microsoft.Teams.Apps";

            try
            {
                var xml = XDocument.Parse(File.ReadAllText(ImpactReviewPipeline.Resolve(workspace, project.Path)));

                if (Path.GetFileNameWithoutExtension(project.Path) == expected ||
                    xml.Descendants().Any(e => e.Name.LocalName is "AssemblyName" or "PackageId" && e.Value.Trim() == expected))
                {
                    if (!projectRoots.TryGetValue(role, out var roots))
                    {
                        projectRoots[role] = roots = [];
                    }

                    roots.Add(project.Path[..(project.Path.LastIndexOf('/') + 1)]);
                }
            }
            catch (System.Xml.XmlException)
            {
                index._unreliableRoles.Add(role);
                index._limitations.Add($"{role}: a project file could not be parsed; source scope requires manual inspection.");
            }
        }

        foreach (var role in files.Select(f => f.Path.Split('/')[0]).Distinct(StringComparer.Ordinal).Where(r => !projectRoots.ContainsKey(r)))
        {
            index._unreliableRoles.Add(role);
            index._limitations.Add($"{role}: package project scope was not resolved; registration discovery uses namespace hints and requires manual inspection.");
        }

        foreach (var file in files.Where(f => f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var role = file.Path.Split('/')[0];
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(ImpactReviewPipeline.Resolve(workspace, file.Path)),
                new CSharpParseOptions(LanguageVersion.Preview));

            if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                diagnostics.Add(role);
            }

            if (tree.GetRoot().DescendantTrivia(descendIntoTrivia: true).Any(t => t.GetStructure() is IfDirectiveTriviaSyntax))
            {
                conditionalRoles.Add(role);
            }

            foreach (var node in tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var name = node switch
                {
                    BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                    DelegateDeclarationSyntax handler => handler.Identifier.ValueText,
                    MethodDeclarationSyntax method => method.Identifier.ValueText,
                    PropertyDeclarationSyntax property => property.Identifier.ValueText,
                    _ => null
                };

                if (name is null) continue;

                var ancestors = node.Ancestors().Reverse().ToArray();
                var ns = string.Join(".", ancestors.OfType<BaseNamespaceDeclarationSyntax>().Select(n => n.Name.ToString()));
                var containers = ancestors.OfType<BaseTypeDeclarationSyntax>().Select(t => t.Identifier.ValueText).ToArray();
                var symbol = string.Join(".", new[] { ns }.Concat(containers).Append(name).Where(p => p.Length > 0));
                var span = node.GetLocation().GetLineSpan();
                var publicApi = node.Modifiers.Any(SyntaxKind.PublicKeyword) ||
                    (node.Parent is InterfaceDeclarationSyntax && !node.Modifiers.Any(t =>
                        t.IsKind(SyntaxKind.PrivateKeyword) || t.IsKind(SyntaxKind.InternalKeyword) || t.IsKind(SyntaxKind.ProtectedKeyword)));

                if (node is BaseTypeDeclarationSyntax && publicApi)
                {
                    index._publicTypes.Add(role + ":" + symbol);
                }

                var registration = node is MethodDeclarationSyntax && publicApi && name.Length > 2 &&
                    name.StartsWith("On", StringComparison.Ordinal) && char.IsUpper(name[2]) &&
                    !ns.Split('.').Any(p => p is "Tests" or "Test" or "Samples") &&
                    (projectRoots.TryGetValue(role, out var roots) ? roots.Any(root => file.Path.StartsWith(root, StringComparison.Ordinal)) :
                        role == "agents" ? Within(ns, "Microsoft.Agents.Extensions.MSTeams") : Within(ns, "Microsoft.Teams.Apps"));
 
                var enclosingTypes = Enumerable.Range(1, containers.Length)
                    .Select(count => role + ":" + string.Join(".", new[] { ns }.Concat(containers.Take(count)).Where(p => p.Length > 0))).ToArray();
                index._declarations.Add(new(role, symbol, registration, enclosingTypes,
                    new(file.Path, StartLine: span.StartLinePosition.Line + 1, EndLine: span.EndLinePosition.Line + 1)));
            }
        }
 
        foreach (var role in diagnostics.Order(StringComparer.Ordinal))
        {
            index._unreliableRoles.Add(role);
            index._limitations.Add($"{role}: syntax diagnostics were encountered; discovery is a navigation aid, not a compiled or exhaustive API inventory.");
        }
 
        foreach (var role in conditionalRoles.Order(StringComparer.Ordinal))
        {
            index._unreliableRoles.Add(role);
            index._limitations.Add($"{role}: conditional compilation requires manual inspection; this index does not evaluate target-framework/build symbols.");
        }
 
        return index;
    }

    private static bool Within(string ns, string prefix) => ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal);
 
    private static bool Matches(string symbol, string hint) => symbol == hint || symbol.EndsWith("." + hint, StringComparison.Ordinal);

    internal bool HasSymbolMatches(ErgonomicsSurface surface) => _declarations.Any(d =>
        d.Role == surface.Role && surface.Symbols.Any(h => Matches(d.Symbol, h)));

    internal bool CanEstablishAbsence(ErgonomicsSurface surface) => surface.Symbols.Count > 0
        ? !_unreliableRoles.Contains(surface.Role) && !HasSymbolMatches(surface)
        : Candidates(surface).Count == 0;

    internal IReadOnlyList<ErgonomicsSymbolLocation> Candidates(ErgonomicsSurface surface)
    {
        var matches = _declarations.Where(d => d.Role == surface.Role && surface.Symbols.Any(h => Matches(d.Symbol, h))).ToArray();
 
        if (matches.Length > 0)
        {
            return matches.Select(d => new ErgonomicsSymbolLocation(d.Symbol, d.Evidence)).ToArray();
        }

        // Existing paths are hints, never mandatory source locations. Documentation may move too.
        var paths = _files.Where(f => f.Path.StartsWith(surface.Role + "/", StringComparison.Ordinal) &&
            (surface.PathHints.Contains(f.Path, StringComparer.Ordinal) ||
             (surface.Symbols.Count == 0 && surface.PathHints.Any(h => Path.GetFileName(h) == Path.GetFileName(f.Path)))))
            .Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        var declarations = _declarations.Where(d => paths.Contains(d.Evidence.Path))
            .Select(d => new ErgonomicsSymbolLocation(d.Symbol, d.Evidence)).ToList();

        if (surface.Symbols.Count == 0)
        {
            declarations.AddRange(paths.Select(p => new ErgonomicsSymbolLocation("", new(p, StartLine: 1, EndLine: 1))));
        }

        return declarations;
    }

    internal bool Binds(ErgonomicsSurface surface, ErgonomicsSurfaceEvidence resolution, IReadOnlyList<ReviewCitation> evidence)
    {
        if (resolution.ResolvedSymbols is null)
        {
            return false;
        }

        if (resolution.ResolvedSymbols.Count == 0)
        {
            return surface.Symbols.Count == 0 && evidence.Any(c => Candidates(surface).Any(d => d.Evidence.Path == c.Path));
        }
        // Renamed APIs can be resolved explicitly to real declarations in the correct source role.
        // The resolution prose must explain their role; semantic equivalence remains a review judgment.
        return resolution.ResolvedSymbols.All(symbol =>
            (surface.Symbols.Count == 0 && symbol.StartsWith("file:" + surface.Role + "/", StringComparison.Ordinal) &&
                _files.Any(f => f.Path == symbol[5..]) && evidence.Any(c => c.Path == symbol[5..])) ||
            _declarations.Any(d => d.Role == surface.Role && d.Symbol == symbol && evidence.Any(c => Overlaps(c, d.Evidence))));
    }

    internal static bool Overlaps(ReviewCitation citation, ReviewCitation declaration) =>
        citation.Path == declaration.Path && citation.StartLine <= declaration.EndLine && citation.EndLine >= declaration.StartLine;

    internal ErgonomicsNavigation Navigate(IReadOnlyList<ErgonomicsOperation> operations)
    {
        var discovery = _declarations.Where(d => d.Registration && d.Containers.All(_publicTypes.Contains))
            .GroupBy(d => (Scope: d.Role == "agents" ? "agents" : "teams", d.Symbol))
            .Select(group => new ErgonomicsDiscoveredOperation(group.Key.Scope + ":" + group.Key.Symbol, group.Key.Symbol,
                group.Key.Scope == "agents" ? "agents" :
                !group.Any(d => d.Role == "teams-baseline") ? "candidate-added" :
                !group.Any(d => d.Role == "teams-candidate") ? "candidate-removed" : "baseline-and-candidate",
                group.Select(d => d.Evidence).OrderBy(c => c.Path, StringComparer.Ordinal).ThenBy(c => c.StartLine).ToArray()))
            .OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();

        var surfaces = operations.SelectMany(o => o.Surfaces.Select(s => new ErgonomicsSurfaceLocation(o.Id, s.Id, Candidates(s)))).ToArray();
        var indexed = surfaces.SelectMany(s => s.Candidates.Select(c => c.Evidence)).Concat(discovery.SelectMany(d => d.Evidence))
            .DistinctBy(c => (c.Path, c.StartLine, c.EndLine))
            .OrderBy(c => c.Path, StringComparer.Ordinal).ThenBy(c => c.StartLine).ThenBy(c => c.EndLine)
            .Select((citation, i) => new ErgonomicsIndexedCitation($"ev{i + 1:D4}", citation)).ToArray();
        var references = indexed.ToDictionary(c => (c.Citation.Path, c.Citation.StartLine, c.Citation.EndLine), c => c.Id);
        return new(surfaces.Select(s => s with
            {
                Candidates = s.Candidates.Select(c => c with { Reference = references[(c.Evidence.Path, c.Evidence.StartLine, c.Evidence.EndLine)] }).ToArray()
            }).ToArray(),
            discovery.Select(d => d with
            {
                EvidenceReferences = d.Evidence.Select(c => references[(c.Path, c.StartLine, c.EndLine)]).ToArray()
            }).ToArray(),
            ["Registration discovery indexes public On* declarations in MSTeams and Microsoft.Teams.Apps; other registration conventions require source inspection.", .. _limitations], indexed);
    }
}

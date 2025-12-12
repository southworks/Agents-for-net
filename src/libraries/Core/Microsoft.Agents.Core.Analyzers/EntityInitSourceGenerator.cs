using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    [Generator]
    public class EntityInitSourceGenerator : IIncrementalGenerator
    {
        internal const string EntityInitAttributeFullName = "Microsoft.Agents.Core.Serialization.EntityNameAttribute";
        internal const string EntityInitAssemblyAttributeFullName = "Microsoft.Agents.Core.Serialization.EntityInitAssemblyAttribute";    

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<ImmutableArray<string?>> types =
                context.SyntaxProvider
                    .ForAttributeWithMetadataName(
                        EntityInitAttributeFullName,
                        (node, _) => node is ClassDeclarationSyntax || node is StructDeclarationSyntax,
                        (context, ct) =>
                            // If need be - this can also return some diagnostics objects (like a warning if decorated type doesn't have static Init method, etc)
                            // Those diagnostics can be emitted when registering source output below
                            (context.TargetSymbol as INamedTypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    )
                    .Where(static x => x is not null)
                    .Collect();

            context.RegisterSourceOutput(types, static (context, types) =>
            {
                var source = string.Join("\r\n", types.Select(x => $"[assembly: {EntityInitAssemblyAttributeFullName}(typeof({x}))]"));

                context.AddSource("EntityInitAssemblyAttribute.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }
    }

}
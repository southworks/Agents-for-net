// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Creates assembly EntityInitAssemblyAttribute to register all Entity subclasses for serialization.
    /// </summary>
    [Generator]
    public class EntityInitSourceGenerator : IIncrementalGenerator
    {
        internal const string EntityTypeFullName = "Microsoft.Agents.Core.Models.Entity";
        internal const string EntityInitAssemblyAttributeFullName = "Microsoft.Agents.Core.Serialization.EntityInitAssemblyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Step 1: Get all class declarations in the compilation
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
                )
                .Where(static cls => cls != null);

            // Step 2: Combine with Compilation to resolve symbols
            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            // Step 3: Process and generate code
            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) =>
            {
                var (compilation, classes) = source;

                var baseTypeSymbol = compilation.GetTypeByMetadataName(EntityTypeFullName);
                if (baseTypeSymbol == null)
                    return; // Base class not found

                var subclasses = new List<INamedTypeSymbol>();
                foreach (var classDecl in classes)
                {
                    var model = compilation.GetSemanticModel(classDecl.SyntaxTree);
                    var symbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                    if (symbol == null)
                        continue;

                    // Check if it inherits from the base type
                    if (symbol.InheritsFrom(baseTypeSymbol))
                    {
                        subclasses.Add(symbol);
                    }
                }

                if (subclasses.Count > 0)
                {
                    var entityAttributes = string.Join("\r\n", subclasses.Select(x => $"[assembly: {EntityInitAssemblyAttributeFullName}(typeof({x.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]"));
                    spc.AddSource("EntityInitAssemblyAttribute.g.cs", SourceText.From(entityAttributes, Encoding.UTF8));
                }
            });
        }
    }
}
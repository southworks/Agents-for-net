// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Diagnostics;
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
            if (!Debugger.IsAttached)
            {
                //Debugger.Launch();
            }

            // Step 1: get the Compilation
            var compilationProvider = context.CompilationProvider;

            // Step 2: resolve Entity type
            var myTypeProvider = compilationProvider
                .Select(static (compilation, _) => compilation.GetTypeByMetadataName(EntityTypeFullName));

            // Step 3: find derived types
            var derivedTypesProvider =
                compilationProvider
                    .Combine(myTypeProvider)
                    .Select(static (pair, ct) => FindDerivedTypes(pair.Left, pair.Right));

            // Step 4: generate EntityInitAssemblyAttributes for found derived types
            context.RegisterSourceOutput(
                derivedTypesProvider,
                static (spc, derivedTypes) =>
                {
                    if (derivedTypes.IsDefaultOrEmpty)
                    {
                        return;
                    }

                    var entityAttributes = string.Join("\r\n", derivedTypes.Select(x => $"[assembly: {EntityInitAssemblyAttributeFullName}(typeof({x.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]"));
                    spc.AddSource("EntityInitAssemblyAttribute.g.cs", SourceText.From(entityAttributes, Encoding.UTF8));
                });
        }

        private static ImmutableArray<INamedTypeSymbol> FindDerivedTypes(
            Compilation compilation,
            INamedTypeSymbol? myType)
        {
            if (myType is null)
                return ImmutableArray<INamedTypeSymbol>.Empty;

            var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

            CollectAnyDerivedType(compilation.Assembly.GlobalNamespace, myType, builder);

            return builder.ToImmutable();
        }

        private static void CollectAnyDerivedType(
            INamespaceSymbol ns,
            INamedTypeSymbol baseType,
            ImmutableArray<INamedTypeSymbol>.Builder builder)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    CollectAnyDerivedType(childNs, baseType, builder);
                }
                else if (member is INamedTypeSymbol type)
                {
                    if (type.InheritsFrom(baseType))
                    {
                        builder.Add(type);
                    }
                }
            }
        }
    }
}
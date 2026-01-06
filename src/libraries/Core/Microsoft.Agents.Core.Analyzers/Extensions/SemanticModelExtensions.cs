// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Threading;

namespace Microsoft.Agents.Core.Analyzers.Extensions
{
    internal static class SemanticModelExtensions
    {
        public static ITypeSymbol? TryParseType(this SemanticModel semanticModel, string typeAsString)
        {
            var typeSyntax = SyntaxFactory.ParseTypeName(typeAsString);

            // Resolve the type symbol
            var typeSymbol = semanticModel.GetSpeculativeTypeInfo(0, typeSyntax, SpeculativeBindingOption.BindAsTypeOrNamespace).Type;

            return typeSymbol;
        }

        public static SymbolInfo GetSymbolInfoSafe(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) // to match default parameter of GetSymbolInfo
        {
            if (node.SyntaxTree != semanticModel.SyntaxTree)
            {
#pragma warning disable RS1030 // Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer
                semanticModel = semanticModel.Compilation.GetSemanticModel(node.SyntaxTree);
#pragma warning restore RS1030 // Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer
            }

            var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
            return symbolInfo;
        }
    }
}
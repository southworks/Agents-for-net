// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Core.Analyzers.Extensions
{
    internal static class SymbolExtensions
    {
        public static bool IsMock(this ISymbol? symbol)
        {
            return symbol?.TryGetType()?.ToDisplayString().StartsWith("Moq.Mock<") ?? false;
        }

        public static bool HasGenericDelegateType(this ISymbol? symbol)
        {
            return symbol?.TryGetType() is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Any(x => x.TypeKind == TypeKind.Delegate);
        }

        public static bool HasGenericILoggerParameter(this ISymbol? symbol)
        {
            return symbol?.TryGetType() is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Any(x => x.IsILogger());
        }

        public static bool HasGenericHttpParameter(this ISymbol? symbol)
        {
            return symbol?.TryGetType() is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Any(x => x.IsHttp());
        }

        private static ITypeSymbol? TryGetType(this ISymbol? symbol) => symbol switch
        {
            ITypeSymbol typeSymbol => typeSymbol,
            IPropertySymbol propertySymbol => propertySymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            ILocalSymbol localSymbol => localSymbol.Type,
            IParameterSymbol parameterSymbol => parameterSymbol.Type,
            IEventSymbol eventSymbol => eventSymbol.Type,
            IMethodSymbol methodSymbol => methodSymbol.ContainingType,
            _ => null
        };

        public static bool IsILogger(this ITypeSymbol? namedTypeSymbol)
        {
            if (namedTypeSymbol == null)
            {
                return false;
            }
            return namedTypeSymbol.Name == "ILogger" && namedTypeSymbol.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.Extensions.Logging");
        }

        public static bool IsHttp(this ITypeSymbol? namedTypeSymbol)
        {
            if (namedTypeSymbol == null)
            {
                return false;
            }
            return namedTypeSymbol.Name.StartsWith("Http") && namedTypeSymbol.ContainingNamespace.ToDisplayString().StartsWith("System.Net.Http");
        }

        public static bool IsMockOf(this ISymbol? symbol, ITypeSymbol type, SemanticModel semanticModel)
        {
            if (TryGetType(symbol) is INamedTypeSymbol { IsGenericType: true } mockType && mockType.IsMock())
            {
                return semanticModel.Compilation.ClassifyCommonConversion(mockType.TypeArguments[0], type) is { IsImplicit: true, Exists: true };
            }
            return false;
        }

        public static bool IsMockOf(this ISymbol? symbol, string typeAsString, SemanticModel semanticModel)
        {
            if (semanticModel.TryParseType(typeAsString) is ITypeSymbol typeSymbol)
            {
                return symbol.IsMockOf(typeSymbol, semanticModel);
            }
            return false;
        }

        public static IEnumerable<ISymbol> GetAllMembers(this ITypeSymbol typeSymbol, string memberName, string parameters)
        {
            var members = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            // Add members of the current type
            members.UnionWith(typeSymbol.GetMembers(memberName));

            // Add members from base types
            var baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                members.UnionWith(baseType.GetMembers(memberName));
                baseType = baseType.BaseType;
            }

            // Add members from implemented interfaces
            foreach (var interfaceType in typeSymbol.AllInterfaces)
            {
                members.UnionWith(interfaceType.GetMembers(memberName));
            }

            return members;
        }

        public static IEnumerable<ISymbol> GetAllMembersThatMatch(this ITypeSymbol typeSymbol, string memberName, string parameters)
        {
            var allMembers = typeSymbol.GetAllMembers(memberName, parameters);

            return allMembers.Where(member =>
            {
                // Copied from StackFrame.ToString() method, in order to try to match string representation of a method call
                // Sub optimal, we can in stead catch exception earlier and prettify it, finding out necessary things with reflection
                if (member is IMethodSymbol methodSymbol)
                {
                    IParameterSymbol[] pi = methodSymbol.Parameters.ToArray();
                    string s = "";
                    s += ('(');
                    bool fFirstParam = true;
                    for (int j = 0; j < pi.Length; j++)
                    {
                        if (!fFirstParam)
                            s += (", ");
                        else
                            fFirstParam = false;

                        string typeName = "<UnknownType>";
                        if (pi[j].Type != null)
                            typeName = pi[j].Type.Name;
                        s += (typeName);
                        string? parameterName = pi[j].Name;
                        if (parameterName != null)
                        {
                            s += (' ');
                            s += (parameterName);
                        }
                    }
                    s += (')');
                    return parameters == s;
                }
                return true;
            });
        }

        public static bool HasAttribute(this ISymbol symbol, ISymbol attributeSymbol) =>
            symbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attributeSymbol, attr.AttributeClass));

        public static bool IsFromCurrentRepository(this ISymbol ns)
        {
            return ns.ContainingAssembly is null || // note, for some symbols it can be null, if it's shared across multiple assemblies. May be namespaces can?
                   ns.ContainingAssembly.GetAttributes().Any(a => a.ToString() == "System.Reflection.AssemblyProductAttribute(\"Microsoft Power Virtual Agents\")");
        }

        public static bool InheritsFrom(this ITypeSymbol classSymbol, ISymbol? baseClassSymbol)
        {
            var current = classSymbol.BaseType;
            while (current != null)
            {
                if (current.Equals(baseClassSymbol, SymbolEqualityComparer.Default))
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }
    }
}
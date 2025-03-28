using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Agents.Connector.HeaderPropagation
{
    public class HeaderPropagationAttribute : Attribute
    {
        internal static IHeaderDictionary SetHeadersSerialization()
        {
            //init newly loaded assemblies
            AppDomain.CurrentDomain.AssemblyLoad += (s, o) => SetHeadersAssembly(o.LoadedAssembly);
            //and all the ones we currently have loaded

            var combinedHeaders = new HeaderDictionary();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                //yield return SetHeadersAssembly(assembly);
                var headers = SetHeadersAssembly(assembly);
                if (headers != null)
                {
                    var tempDictionary = headers;

                    foreach (var kvp in tempDictionary)
                    {
                        combinedHeaders[kvp.Key] = kvp.Value;
                    }
                }
            }

            return combinedHeaders;
        }

        private static IDictionary<string, string> SetHeadersAssembly(Assembly assembly)
        {
            //foreach (var type in GetLoadOnsetHeadersTypes(assembly))
            //{
            //    var setHeaders = type.GetMethod("SetHeaders", BindingFlags.Static | BindingFlags.Public);
            //    if (setHeaders != null)
            //    {
            //        yield return setHeaders.Invoke(assembly, null);
            //    }
            //}

            var combinedDictionary = new Dictionary<string, string>();

            foreach (var type in GetLoadOnsetHeadersTypes(assembly))
            {
                var setHeaders = type.GetMethod("SetHeaders", BindingFlags.Static | BindingFlags.Public);
                if (setHeaders != null)
                {
                    var tempDictionary = setHeaders.Invoke(assembly, null);

                    foreach (var kvp in tempDictionary as IDictionary<string, string>)
                    {
                        combinedDictionary[kvp.Key] = kvp.Value;
                    }
                }
            }

            return combinedDictionary;

        }

        private static IEnumerable<Type> GetLoadOnsetHeadersTypes(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HeaderPropagationAttribute), true).Length > 0)
                {
                    yield return type;
                }
            }
        }
    }
}

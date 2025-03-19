// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore.HeaderPropagation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Custom implementation of an IHttpClientFactory that allows customizing the settings in the <see cref="HttpClient"/> instance.
    /// </summary>
    /// <param name="provider">Service provider used to gather DI related classes.</param>
    /// <param name="factory">Existing HttpClientFactory to use in this implementation.</param>
    public class AgentsHttpClientFactory(IServiceProvider provider = null, IHttpClientFactory factory = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            var client = factory?.CreateClient(name) ?? new HttpClient();

            var headers = provider.GetService<HeaderPropagationContext>()?.Headers ?? new HeaderDictionary();
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, [header.Value]);
            }

            return client;
        }
    }
}

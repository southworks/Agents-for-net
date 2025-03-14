// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Custom implementation of IHttpClientFactory that allows for the propagation of headers from the current request to the HttpClient.
    /// </summary>
    /// <param name="provider">Service provider used to gather DI related classes.</param>
    /// <param name="factory">Existing HttpClientFactory to use in this implementation.</param>
    public class AgentsHttpClientFactory(IServiceProvider provider = null, IHttpClientFactory factory = null) : IHttpClientFactory
    {
        private readonly IHttpClientFactory _factory = factory;
        private readonly AsyncLocal<IHeaderDictionary> _requestHeaders = new AsyncLocal<IHeaderDictionary>();
        private readonly List<string> _filteredHeaderKeys = provider.GetService<IOptions<HeaderPropagationOptions>>()?.Value?.Headers ?? new List<string>();

        public HttpClient CreateClient(string name)
        {
            var client = _factory?.CreateClient(name) ?? new HttpClient();

            if (_requestHeaders.Value != null)
            {
                foreach (var header in _requestHeaders.Value)
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, new List<string> { header.Value });
                }
            }

            return client;
        }

        public void AddHeaders(IHeaderDictionary headers)
        {
            _requestHeaders.Value ??= new HeaderDictionary();
            foreach (var key in _filteredHeaderKeys)
            {
                var header = headers.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
                if (header.Key != null)
                {
                    _requestHeaders.Value.TryAdd(header.Key, header.Value);
                }
            }
        }
    }
}

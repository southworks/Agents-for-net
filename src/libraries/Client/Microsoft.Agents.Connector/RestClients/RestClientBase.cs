// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml.Linq;
using Microsoft.Agents.Connector.HeaderPropagation;

namespace Microsoft.Agents.Connector.RestClients
{
    public class RestClientBase(Uri endpoint, IHttpClientFactory httpClientFactory, string httpClientName, Func<Task<string>> tokenProviderFunction, IServiceProvider provider = null) : IRestTransport
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly Func<Task<string>> _tokenProviderFunction = tokenProviderFunction;
        private readonly string _httpClientName = httpClientName ?? throw new ArgumentNullException(nameof(httpClientName));
        private readonly Uri _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        private readonly IServiceProvider _provider = provider; 

        public Uri Endpoint => _endpoint;

        public async Task<HttpClient> GetHttpClientAsync()
        {
            var httpClient = _httpClientFactory.CreateClient(_httpClientName);

            if (_tokenProviderFunction != null)
            {
                var token = await _tokenProviderFunction().ConfigureAwait(false);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            httpClient.AddDefaultUserAgent();

            SetCustomHeaders(httpClient);

            return httpClient;
        }

        private void SetCustomHeaders(HttpClient client)
        {
            var headers = _provider.GetService<HeaderPropagationContext>()?.Headers ?? new HeaderDictionary();
            
            //logger.LogDebug("Found {HeaderCount} headers to propagate.", headers.Count);
            
            foreach (var header in headers)
            {
                if (client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, [header.Value]))
                {
                    //logger.LogWarning("Failed to add header {HeaderName} to HTTP client.", header.Key);
                }
                else
                {
                    //logger.LogTrace("Added header {HeaderName} to HTTP client.", header.Key);
                }
            }
        }
    }
}

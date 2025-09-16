// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.A365;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.AgenticDemo
{
    public static class AgenticGraphClient
    {
        public static GraphServiceClient GraphClientForAgentUser(this AgenticAuthorization a365, ITurnContext turnContext, IList<string> scopes)
        {
            try
            {
                // Create an async token provider that calls GetAgenticUserTokenAsync each time
                var authProvider = new ManualTokenAuthenticationProvider(() =>
                {
                    return a365.GetAgenticUserTokenAsync(turnContext, scopes);
                });
                return new GraphServiceClient(authProvider, baseUrl: "https://canary.graph.microsoft.com/testprodbetateamsgraphdev");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating GraphServiceClient with agentic user identity: {ex.Message}");
                throw;
            }
        }
    }

    class ManualTokenAuthenticationProvider(Func<Task<string>> AccessTokenProvider) : IAuthenticationProvider
    {
        public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            var accessToken = await AccessTokenProvider();
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
        }
    }
}

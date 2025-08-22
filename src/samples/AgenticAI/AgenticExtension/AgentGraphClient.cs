// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.AgenticExtension
{
    public static class AgentGraphClient
    {
        public static GraphServiceClient GraphClientForAgentUser(this AgentAuthorization agentAuthorization, ITurnContext turnContext, IList<string> scopes)
        {
            try
            {
                // Create an async token provider that calls GetAgenticUserTokenAsync each time
                var authProvider = new ManualTokenAuthenticationProvider(() =>
                {
                    return agentAuthorization.GetAgentUserTokenAsync(turnContext, scopes);
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

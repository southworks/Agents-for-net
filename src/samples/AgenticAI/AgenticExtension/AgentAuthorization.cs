// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

namespace AgenticAI.AgenticExtension
{
    /*
      Demo Activity.Recipient

        "recipient":
        {
          "id":"34bde265-6abe-4392-9f2a-90063f156f4a", // AA
          "name":"saapp1user1@projectkairoentra.onmicrosoft.com", //AU UPN
          "aadObjectId":"cc8beb3e-8e7a-4f33-91da-08c612099a58", // AU Oid
          "aadClientId":"52fb5abc-26cb-4ede-b26c-0aa4c1f2154c", // AAI
          "role":"agentuser"
        } 
    */

    public class AgentAuthorization
    {
        private readonly IConnections _connections;
        private readonly IHttpClientFactory _httpClientFactory;
        //private readonly static MemoryCache _cache = new MemoryCache(nameof(AgenticAuthorization));

        public static readonly RouteSelector AgenticAIMessage = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Message && IsAgenticRequest(tc));
        public static readonly RouteSelector AgenticAIEvent = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Event && IsAgenticRequest(tc));

        public AgentAuthorization(IConnections connections, IHttpClientFactory httpClientFactory = null)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));

            _connections = connections;
            _httpClientFactory = httpClientFactory;
        }

        public static bool IsAgenticRequest(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            return IsAgenticRequest(turnContext.Activity);
        }

        public static bool IsAgenticRequest(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            return activity?.Recipient?.Role == RoleTypes.AgentIdentity
                || activity?.Recipient?.Role == RoleTypes.AgentUser;
        }

        public static string GetAgentInstanceId(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.AadClientId;
        }

        public static string GetAgentUser(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.Name;  // What the demo is using for AU UserUpn
        }

        // AA -> AAI token
        public async Task<string> GetAgentInstanceTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext))
            {
                return null;
            }

            var (_, instanceTokenResult) = await AAToAAI(turnContext, cancellationToken).ConfigureAwait(false);
            return instanceTokenResult.AccessToken;
        }

        // AA -> AAI -> AAU token
        public async Task<string> GetAgentUserTokenAsync(ITurnContext turnContext, IList<string> scopes, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext) || string.IsNullOrEmpty(GetAgentUser(turnContext)))
            {
                return null;
            }

            var msalProvider = GetMsalProvider(turnContext);  // can throw

            var (agentTokenResult, instanceTokenResult) = await AAToAAI(turnContext, cancellationToken).ConfigureAwait(false);

            // THIRD: Get combined user token
            var userToken = await AAIToAAU(
                GetAgentInstanceId(turnContext),
                msalProvider.ConnectionSettings.TenantId,  // TurnContext.Identity doesn't have tenant for ABS requests
                agentTokenResult.AccessToken,
                instanceTokenResult.AccessToken,
                GetAgentUser(turnContext),
                scopes);

            return userToken;
        }

        private IMSALProvider GetMsalProvider(ITurnContext turnContext)
        {
            // Using ConnectionMap to get Connection for request identity
            var connection = _connections.GetTokenProvider(turnContext.Identity, "agentic");

            if (connection is not IMSALProvider msalProvider)
            {
                throw new InvalidOperationException($"MSAL Connection for identity '{AgentClaims.GetAppId(turnContext.Identity)}' not found");
            }

            return msalProvider;
        }

        // returns tuple (agentTokenResult, agentInstanceResult)
        private async Task<(AuthenticationResult, AuthenticationResult)> AAToAAI(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // TODO: use MemoryCache for both results

            var agentAppInstanceId = GetAgentInstanceId(turnContext);
            var msalProvider = GetMsalProvider(turnContext);
            var connectionSettings = msalProvider.ConnectionSettings;

            IConfidentialClientApplication msalApplication = msalProvider.CreateClientApplication() as IConfidentialClientApplication
                ?? throw new InvalidOperationException($"Connection for identity '{AgentClaims.GetAppId(turnContext.Identity)}' is not IConfidentialClientApplication");

            //TODO: handle exceptions

            // FIRST: Get token for AA
            var agentTokenResult = await msalApplication
                .AcquireTokenForClient(["api://AzureAdTokenExchange/.default"]).WithFmiPath(agentAppInstanceId)
                .ExecuteAsync(cancellationToken).ConfigureAwait(false);

            // SECOND: Use AA token to get AAI token
            var instanceApp = ConfidentialClientApplicationBuilder
                .Create(agentAppInstanceId)
                .WithClientAssertion((AssertionRequestOptions options) => Task.FromResult(agentTokenResult.AccessToken))
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{connectionSettings.TenantId}"))
                .Build();

            return (agentTokenResult, await instanceApp
                .AcquireTokenForClient(["api://AzureAdTokenExchange/.default"])
                .ExecuteAsync(cancellationToken).ConfigureAwait(false));
        }

        private async Task<string> AAIToAAU(
            string clientId,
            string tenantId,
            string clientAssertion,
            string userFederatedIdentityCredential,
            string username,
            IList<string> scopes)
        {
            // TODO: use MemoryCache?

            using var httpClient = _httpClientFactory?.CreateClient(nameof(AgentAuthorization)) ?? new HttpClient();

            var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

            var parameters = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "scope", string.Join(" ", scopes) },
                { "client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer" },
                { "client_assertion", clientAssertion },
                { "username", username },
                { "user_federated_identity_credential", userFederatedIdentityCredential },
                { "grant_type", "user_fic" }
            };

            var content = new FormUrlEncodedContent(parameters);

            // TODO: exception handling
            var response = await httpClient.PostAsync(tokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to acquire user federated identity token: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);

            if (tokenResponse != null && tokenResponse.TryGetValue("access_token", out var accessToken))
            {
                return accessToken?.ToString() ?? throw new InvalidOperationException("Access token is null");
            }

            throw new InvalidOperationException("Failed to parse access token from response");
        }
    }
}

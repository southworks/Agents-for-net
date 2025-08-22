using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

namespace AgenticAI
{
    public class AgentAuthorization
    {
        private readonly IConnections _connections;
        private readonly IHttpClientFactory _httpClientFactory;
        //private readonly static MemoryCache _cache = new MemoryCache(nameof(AgenticAuthorization));

        public AgentAuthorization(IConnections connections, IHttpClientFactory httpClientFactory = null)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));

            _connections = connections;
            _httpClientFactory = httpClientFactory;
        }

        public bool IsAgenticRequest(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            return turnContext.Activity?.Recipient?.Role == RoleTypes.AgentIdentity
                || turnContext.Activity?.Recipient?.Role == RoleTypes.AgentUser;
        }

        public string GetAgentInstanceId(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.AadClientId;
        }

        public string GetAgentUser(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.Name;  // What the demo is using for AU UserUpn
        }

        // AA -> AAI token
        public async Task<TokenResponse> GetAgentInstanceTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext))
            {
                return null;
            }

            var (agentTokenResult, instanceTokenResult) = await AAToAAI(turnContext, cancellationToken).ConfigureAwait(false);
            return new TokenResponse()
            {
                Token = instanceTokenResult.AccessToken,
                IsExchangeable = true,
                Expiration = instanceTokenResult.ExpiresOn
            };
        }

        // AA -> AAI -> AAU token
        public async Task<TokenResponse> GetAgentUserTokenAsync(ITurnContext turnContext, IList<string> scopes, CancellationToken cancellationToken = default)
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
                msalProvider.ConnectionSettings.TenantId,  // can get from TurnContext.Identity?
                agentTokenResult.AccessToken,
                instanceTokenResult.AccessToken,
                GetAgentUser(turnContext),
                scopes);

            return new TokenResponse()
            {
                Token = userToken,
                IsExchangeable = true
            };
        }

        private IMSALProvider GetMsalProvider(ITurnContext turnContext)
        {
            // Using ConnectionMap to get Connection for request identity
            var connection = _connections.GetTokenProvider(turnContext.Identity, null);

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

            using var httpClient = _httpClientFactory?.CreateClient(nameof(AgenticAuthorization)) ?? new HttpClient();

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

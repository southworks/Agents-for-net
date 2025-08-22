// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI;

#nullable disable

public class AgenticAuthorization : IUserAuthorization
{
    private readonly AgenticAuthSettings _settings;
    private readonly IConnections _connections;
    private readonly IHttpClientFactory _httpClientFactory = null;
    //private readonly static MemoryCache _cache = new MemoryCache(nameof(AgenticAuthorization));

    public AgenticAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection)
    {
        Name = name;
        _connections = connections;
        _settings = configurationSection.Get<AgenticAuthSettings>()!;
    }

    public string Name { get; private set; }

    public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
    {
        if (turnContext.Activity?.Recipient?.Role != RoleTypes.AgentIdentity)
        {
            throw new InvalidOperationException($"Unexpected Activity.Recipient.Role: {turnContext.Activity!.Recipient.Role}");
        }

        var agentAppInstanceId = turnContext.Activity?.Recipient?.AadClientId;
        if (string.IsNullOrWhiteSpace(agentAppInstanceId))
        {
            throw new InvalidOperationException("Missing Activity.Recipient.AadClientId");
        }

        var connection = _connections.GetConnection(_settings.ConnectionName);  // can throw
        if (connection is not IMSALProvider msalProvider)
        {
            throw new InvalidOperationException($"Connection '{_settings.ConnectionName}' is not MSAL");
        }

        if (msalProvider is not IConfidentialClientApplication msalApplication)
        {
            throw new InvalidOperationException($"Connection '{_settings.ConnectionName}' is not IConfidentialClientApplication");
        }

        var connectionSettings = msalProvider.ConnectionSettings;

        /*
        // FIRST: Get AAD token for AgentAppId
        // TODO:  if this works, should just add a new MsalConnectionSettings with "FMIPAth" option.  That way IAccessTokenProvider would work as-is
        var scopes = connectionSettings.Scopes ?? ["api://AzureAdTokenExchange/.default"];
        var agentTokenResult  = await msalApplication
            .AcquireTokenForClient(scopes).WithFmiPath(agentAppInstanceId)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        // SECOND: Get AAD token for AgentAppInstanceId
        var instanceApp = ConfidentialClientApplicationBuilder
            .Create(agentAppInstanceId)
            .WithClientAssertion((AssertionRequestOptions options) => Task.FromResult(agentTokenResult.AccessToken))
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{connectionSettings.TenantId}"))
            .Build();

        var instanceTokenResult = await instanceApp
            .AcquireTokenForClient(["api://AzureAdTokenExchange/.default"])
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        */

        // THIRD: Get combined user token
        var userToken = await GetUserFederatedIdentityTokenAsync(
            agentAppInstanceId,
            connectionSettings.TenantId,
            agentTokenResult.AccessToken,
            instanceTokenResult.AccessToken,
            turnContext.Activity!.From.Id,
            exchangeScopes ?? _settings.Scopes!);

        return new TokenResponse()
        {
            Token = userToken,
            IsExchangeable = true
        };
    }

    public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<TokenResponse> SignInUserAsync(ITurnContext context, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
    {
        var aaiToken = await AAToAAI(context, cancellationToken).ConfigureAwait(false);
        return new TokenResponse()
        {
            Token = aaiToken.AccessToken,
            IsExchangeable = true,
            Expiration = aaiToken.ExpiresOn
        };
    }

    public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private IMSALProvider GetMsalProvider(string connectionName)
    {
        var connection = _connections.GetConnection(_settings.ConnectionName);  // can throw
        if (connection is not IMSALProvider msalProvider)
        {
            throw new InvalidOperationException($"Connection '{_settings.ConnectionName}' is not MSAL");
        }
        return msalProvider;
    }

    private async Task<AuthenticationResult> AAToAAI(ITurnContext turnContext, CancellationToken cancellationToken)
    {
        if (turnContext.Activity?.Recipient?.Role != RoleTypes.AgentIdentity)
        {
            throw new InvalidOperationException($"Unexpected Activity.Recipient.Role: {turnContext.Activity!.Recipient.Role}");
        }

        if (string.IsNullOrWhiteSpace(turnContext.Activity?.Recipient?.AadClientId))
        {
            throw new InvalidOperationException("Missing Activity.Recipient.AadClientId");
        }

        var agentAppInstanceId = turnContext.Activity.Recipient.AadClientId;
        var msalProvider = GetMsalProvider(_settings.ConnectionName);
        var connectionSettings = msalProvider.ConnectionSettings;

        if (msalProvider is not IConfidentialClientApplication msalApplication)
        {
            throw new InvalidOperationException($"Connection '{_settings.ConnectionName}' is not IConfidentialClientApplication");
        }

        //TODO: handle exceptions

        // FIRST: Get token for AA
        // TODO:  if this works, should just add a new MsalConnectionSettings with "FMIPAth" option.  That way IAccessTokenProvider would work as-is
        var scopes = connectionSettings.Scopes ?? ["api://AzureAdTokenExchange/.default"];
        var agentTokenResult = await msalApplication
            .AcquireTokenForClient(scopes).WithFmiPath(agentAppInstanceId)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        // SECOND: Use AA token to get AAI token
        var instanceApp = ConfidentialClientApplicationBuilder
            .Create(agentAppInstanceId)
            .WithClientAssertion((AssertionRequestOptions options) => Task.FromResult(agentTokenResult.AccessToken))
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{connectionSettings.TenantId}"))
            .Build();

        return await instanceApp
            .AcquireTokenForClient(["api://AzureAdTokenExchange/.default"])
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetUserFederatedIdentityTokenAsync(
        string clientId,
        string tenantId, 
        string clientAssertion,
        string userFederatedIdentityCredential,
        string username,
        IList<string> scopes)
    {
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.HeaderPropagation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams;

/// <summary>
/// Provides extension methods for configuring the Teams <see cref="ApiClient"/> within an agents's turn context.
/// </summary>
/// <remarks>
/// These extension methods enable integration with the Teams API by associating an ApiClient
/// instance with the <see cref="ITurnContext"/>. This allows agent developers to access Teams-specific functionality during a
/// conversation turn. The methods support both direct configuration and configuration via an AgentApplication
/// instance.<br/><br/>
/// This creates HttpClients named "TeamsHttpClientFactory".
/// </remarks>
internal static class TeamsApiClientExtensions
{
    /// <summary>
    /// Configures the Teams API client for the specified turn context using the provided agent application
    /// settings.
    /// </summary>
    /// <remarks>This extension method initializes the Teams API client for the given context based on
    /// the application's connection and HTTP client factory settings. Ensure that the application parameter is not
    /// null to avoid configuration errors.</remarks>
    /// <param name="context">The turn context in which to set up the Teams API client.</param>
    /// <param name="application">The agent application containing configuration options for the Teams API client. Cannot be null.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    internal static void SetTeamsApiClient(this ITurnContext context, AgentApplication application, CancellationToken ct = default)
    {
        SetTeamsApiClient(
            context,
            application?.Options?.Connections,
            application?.Options?.HttpClientFactory,
            application?.Options?.LoggerFactory,
            ct);
    }

    /// <summary>
    /// Registers an ApiClient instance for Microsoft Teams API access in the current turn context.
    /// </summary>
    /// <remarks>After calling this method, the registered ApiClient can be retrieved from the
    /// context's service collection for use in subsequent Teams API operations. If the context identity allows
    /// anonymous access, the client will be configured without authentication; otherwise, it will use a token
    /// provider for authenticated requests.</remarks>
    /// <param name="context">The turn context in which to register the Teams ApiClient. Cannot be null.</param>
    /// <param name="connections">The connections provider used to obtain authentication tokens for Teams API requests. Cannot be null.</param>
    /// <param name="httpClientFactory">The factory used to create HTTP clients for communicating with the Teams API. Cannot be null.</param>
    /// <param name="loggerFactory">The logger factory used by the Teams API clients. Cannot be null.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the registration operation. Optional.</param>
    internal static void SetTeamsApiClient(
        this ITurnContext context,
        IConnections connections,
        System.Net.Http.IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken ct = default)
    {
        AssertionHelpers.ThrowIfNull(connections, nameof(connections));
        AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));
        AssertionHelpers.ThrowIfNull(loggerFactory, nameof(loggerFactory));

        bool useAnonymous = AgentClaims.AllowAnonymous(context.Identity);
        var innerHttpClient = httpClientFactory.CreateClient(nameof(TeamsApiClientExtensions));
        AssertionHelpers.ThrowIfNull(innerHttpClient, nameof(httpClientFactory));

        var httpClient = useAnonymous
            ? innerHttpClient
            : new HttpClient(new TeamsAuthenticationHandler(
                innerHttpClient,
                () => connections.GetTokenProvider(context.Identity, context.Activity.ServiceUrl)));
        httpClient.AddDefaultUserAgent();
        httpClient.AddHeaderPropagation();

        var conversationClient = new Microsoft.Teams.Core.ConversationClient(
            httpClient,
            loggerFactory.CreateLogger<Microsoft.Teams.Core.ConversationClient>());
        var userTokenClient = new Microsoft.Teams.Core.UserTokenClient(
            httpClient,
            new ConfigurationBuilder().Build(),
            loggerFactory.CreateLogger<Microsoft.Teams.Core.UserTokenClient>());
        Uri.TryCreate(context.Activity.ServiceUrl, UriKind.Absolute, out var serviceUrl);
        var client = CreateApiClient(
            serviceUrl,
            httpClient,
            conversationClient,
            userTokenClient,
            loggerFactory.CreateLogger<Microsoft.Teams.Apps.Clients.ApiClient>());

        context.Services.Set<Microsoft.Teams.Apps.Clients.ApiClient>(client);
    }

    internal static Microsoft.Teams.Apps.Clients.ApiClient GetTeamsApiClient(this ITurnContext context)
    {
        return context.Services.Get<Microsoft.Teams.Apps.Clients.ApiClient>();
    }

    private static Microsoft.Teams.Apps.Clients.ApiClient CreateApiClient(
        Uri serviceUrl,
        System.Net.Http.HttpClient httpClient,
        Microsoft.Teams.Core.ConversationClient conversationClient,
        Microsoft.Teams.Core.UserTokenClient userTokenClient,
        ILogger logger)
    {
        // Microsoft.Teams.Apps 2.1 documents this constructor but exposes it as internal.
        if (serviceUrl == null)
        {
            var unboundConstructor = typeof(Microsoft.Teams.Apps.Clients.ApiClient).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [
                    typeof(System.Net.Http.HttpClient),
                    typeof(Microsoft.Teams.Core.ConversationClient),
                    typeof(Microsoft.Teams.Core.UserTokenClient),
                    typeof(ILogger)
                ],
                modifiers: null);

            if (unboundConstructor == null)
            {
                throw new MissingMethodException(
                    typeof(Microsoft.Teams.Apps.Clients.ApiClient).FullName,
                    ".ctor(HttpClient, ConversationClient, UserTokenClient, ILogger)");
            }

            return (Microsoft.Teams.Apps.Clients.ApiClient)unboundConstructor.Invoke(
                [httpClient, conversationClient, userTokenClient, logger]);
        }

        var constructor = typeof(Microsoft.Teams.Apps.Clients.ApiClient).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(Uri),
                typeof(System.Net.Http.HttpClient),
                typeof(Microsoft.Teams.Core.ConversationClient),
                typeof(Microsoft.Teams.Core.UserTokenClient),
                typeof(ILogger),
                typeof(Microsoft.Teams.Core.Schema.AgenticIdentity)
            ],
            modifiers: null);

        if (constructor == null)
        {
            throw new MissingMethodException(
                typeof(Microsoft.Teams.Apps.Clients.ApiClient).FullName,
                ".ctor(Uri, HttpClient, ConversationClient, UserTokenClient, ILogger, AgenticIdentity)");
        }

        return (Microsoft.Teams.Apps.Clients.ApiClient)constructor.Invoke(
            [serviceUrl, httpClient, conversationClient, userTokenClient, logger, null]);
    }
}

internal sealed class TeamsAuthenticationHandler(
    HttpClient inner,
    Func<IAccessTokenProvider> tokenProviderFactory) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokenProvider = tokenProviderFactory();
        AssertionHelpers.ThrowIfNull(tokenProvider, nameof(tokenProviderFactory));
        var token = await tokenProvider.GetAccessTokenAsync(
            AuthenticationConstants.BotFrameworkAudience,
            [AuthenticationConstants.BotFrameworkDefaultScope]).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await inner.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

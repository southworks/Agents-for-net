﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core.Pipeline;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Protocols.Primitives;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Protocols.Serializer;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.Agents.Connector.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]

namespace Microsoft.Agents.Protocols.Connector
{
    /// <summary>
    /// Client for access user token service.
    /// </summary>
    internal class RestUserTokenClient : IUserTokenClient, IDisposable
    {
        private readonly string _appId;
        private readonly UserTokenRestClient _userTokenClient;
        private readonly BotSignInRestClient _botSignInClient;
        private readonly ILogger _logger;
        private bool _disposed;

        public RestUserTokenClient(string appId, Uri endpoint, IAccessTokenProvider tokenAccess, string audience, List<string> scopes, ILogger logger)
            : this(appId, endpoint, tokenAccess, audience, scopes, new ConnectorClientOptions(), logger)
        {
        }

        public RestUserTokenClient(string appId, Uri endpoint, IAccessTokenProvider tokenAccess, string audience, List<string> scopes, ConnectorClientOptions options, ILogger logger)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(appId);

            _appId = appId;

            // Manually create a pipeline using bearer tokens shared between Attachments and Conversations clients.
            var pipeline = HttpPipelineBuilder.Build(options, new BearerTokenPolicy(tokenAccess, audience, scopes));

            _userTokenClient = new UserTokenRestClient(pipeline, endpoint);
            _botSignInClient = new BotSignInRestClient(pipeline, endpoint);
            _logger = logger;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        /// <inheritdoc />
        public async Task<TokenResponse> GetUserTokenAsync(string userId, string connectionName, string channelId, string magicCode, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetUserTokenAsync));
            }

            _ = userId ?? throw new ArgumentNullException(nameof(userId));
            _ = connectionName ?? throw new ArgumentNullException(nameof(connectionName));

            _logger.LogInformation($"GetTokenAsync ConnectionName: {connectionName}");
            return await _userTokenClient.GetTokenAsync(userId, connectionName, channelId, magicCode, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SignInResource> GetSignInResourceAsync(string connectionName, IActivity activity, string finalRedirect, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetSignInResourceAsync));
            }

            _ = connectionName ?? throw new ArgumentNullException(nameof(connectionName));
            _ = activity ?? throw new ArgumentNullException(nameof(activity));

            _logger.LogInformation($"GetSignInResourceAsync ConnectionName: {connectionName}");
            var state = CreateTokenExchangeState(_appId, connectionName, activity);
            return await _botSignInClient.GetSignInResourceAsync(state, null, null, finalRedirect, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task SignOutUserAsync(string userId, string connectionName, string channelId, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SignOutUserAsync));
            }

            _ = userId ?? throw new ArgumentNullException(nameof(userId));
            _ = connectionName ?? throw new ArgumentNullException(nameof(connectionName));

            _logger.LogInformation($"SignOutAsync ConnectionName: {connectionName}");
            await _userTokenClient.SignOutAsync(userId, connectionName, channelId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TokenStatus[]> GetTokenStatusAsync(string userId, string channelId, string includeFilter, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetTokenStatusAsync));
            }

            _ = userId ?? throw new ArgumentNullException(nameof(userId));
            _ = channelId ?? throw new ArgumentNullException(nameof(channelId));

            _logger.LogInformation("GetTokenStatusAsync");
            var result = await _userTokenClient.GetTokenStatusAsync(userId, channelId, includeFilter, cancellationToken).ConfigureAwait(false);
            return result?.ToArray();
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, TokenResponse>> GetAadTokensAsync(string userId, string connectionName, string[] resourceUrls, string channelId, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetAadTokensAsync));
            }

            _ = userId ?? throw new ArgumentNullException(nameof(userId));
            _ = connectionName ?? throw new ArgumentNullException(nameof(connectionName));

            _logger.LogInformation($"GetAadTokensAsync ConnectionName: {connectionName}");
            return (Dictionary<string, TokenResponse>)await _userTokenClient.GetAadTokensAsync(userId, connectionName, new AadResourceUrls() { ResourceUrls = resourceUrls?.ToList() }, channelId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TokenResponse> ExchangeTokenAsync(string userId, string connectionName, string channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ExchangeTokenAsync));
            }

            _ = userId ?? throw new ArgumentNullException(nameof(userId));
            _ = connectionName ?? throw new ArgumentNullException(nameof(connectionName));

            _logger.LogInformation($"ExchangeAsyncAsync ConnectionName: {connectionName}");
            var result = await _userTokenClient.ExchangeAsyncAsync(userId, connectionName, channelId, exchangeRequest, cancellationToken).ConfigureAwait(false);

            if (result is ErrorResponse errorResponse)
            {
                throw new InvalidOperationException($"Unable to exchange token: ({errorResponse?.Error?.Code}) {errorResponse?.Error?.Message}");
            }
            else if (result is TokenResponse tokenResponse)
            {
                return tokenResponse;
            }
            else
            {
                throw new InvalidOperationException($"ExchangeAsyncAsync returned improper result: {result.GetType()}");
            }
        }

        /// <summary>
        /// Helper function to create the base64 encoded token exchange state used in GetSignInResourceAsync calls.
        /// </summary>
        /// <param name="appId">The appId to include in the token exchange state.</param>
        /// <param name="connectionName">The connectionName to include in the token exchange state.</param>
        /// <param name="activity">The <see cref="Activity"/> from which to derive the token exchange state.</param>
        /// <returns>base64 encoded token exchange state.</returns>
        private static string CreateTokenExchangeState(string appId, string connectionName, IActivity activity)
        {
            _ = appId ?? throw new ArgumentNullException(nameof(appId));
            _ = connectionName ?? throw new ArgumentNullException(nameof(connectionName));
            _ = activity ?? throw new ArgumentNullException(nameof(activity));

            var tokenExchangeState = new TokenExchangeState
            {
                ConnectionName = connectionName,
                Conversation = activity.GetConversationReference(),
                RelatesTo = activity.RelatesTo,
                MsAppId = appId,
            };
            var json = ProtocolJsonSerializer.ToJson(tokenExchangeState);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}

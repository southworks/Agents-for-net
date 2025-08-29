// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.Connector
{
    public class ConnectorUserAuthorization : IUserAuthorization
    {
        private readonly OBOSettings _settings;
        private readonly IConnections _connections;

        public ConnectorUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection)
        {
            _settings = GetOBOSettings(configurationSection);
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; private set; }

        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            var tokenResponse = CreateTokenResponse(turnContext);

            if (tokenResponse.Expiration != null)
            {
                var diff = tokenResponse.Expiration - DateTimeOffset.UtcNow;
                if (diff.HasValue && diff?.TotalMinutes <= 0)
                {
                    // TODO: throw defined error.  Nothing we can do here to get a refreshed token.
                    throw new InvalidOperationException($"Token for '{Name}' is expired");
                }
            }

            return await HandleOBO(turnContext, tokenResponse, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
        }

        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            return await GetRefreshedUserTokenAsync(turnContext, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
        }

        public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // No concept of reset with ConnectorAuth
            return Task.CompletedTask;
        }

        public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // No concept of sign-out with ConnectorAuth
            return Task.CompletedTask;
        }

        private static TokenResponse CreateTokenResponse(ITurnContext turnContext)
        {
            if (turnContext.Identity is CaseSensitiveClaimsIdentity identity)
            {
                var tokenResponse = new TokenResponse()
                {
                    Token = identity.SecurityToken.UnsafeToString(),
                };

                try
                {
                    var jwtToken = new JwtSecurityToken(tokenResponse.Token);
                    tokenResponse.Expiration = jwtToken.ValidTo;
                    tokenResponse.IsExchangeable = AgentClaims.IsExchangeableToken(jwtToken);
                }
                catch (Exception)
                {
                    tokenResponse.IsExchangeable = false;
                }

                return tokenResponse;
            }

            // TODO: throw defined error
            throw new InvalidOperationException();
        }

        // Get the ConnectorUserAuthorization settings
        private static OBOSettings GetOBOSettings(IConfigurationSection config)
        {
            var settings = config.Get<OBOSettings>();

            if (settings.OBOScopes == null)
            {
                // try reading as a string to compensate for users just setting a non-array string
                var configScope = config.GetSection(nameof(OBOSettings.OBOScopes)).Get<string>();
                if (!string.IsNullOrEmpty(configScope))
                {
                    settings.OBOScopes = [configScope];
                }
            }

            return settings;
        }

        // TODO: this is the same code as in AzureBotUserAuthorization.  Refactor.
        private async Task<TokenResponse> HandleOBO(ITurnContext turnContext, TokenResponse token, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token?.Token))
            {
                return null;
            }

            var connectionName = exchangeConnection ?? _settings.OBOConnectionName;
            IList<string> scopes = exchangeScopes ?? _settings.OBOScopes;

            // If OBO is not supplied (by config or passed) return token as-is.
            if (string.IsNullOrEmpty(connectionName) || scopes == null || !scopes.Any())
            {
                return token;
            }

            try
            {
                // Can we even exchange this?
                if (!token.IsExchangeable)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBONotExchangeableToken, null, [connectionName]);
                }

                // Can the named Connection even do this?
                if (!TryGetOBOProvider(connectionName, out var oboExchangeProvider))
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBONotSupported, null, [connectionName]);
                }

                // Do exchange.
                try
                {
                    token = await oboExchangeProvider.AcquireTokenOnBehalfOf(scopes, token.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, ex, [connectionName, string.Join(",", scopes)]);
                }

                if (token == null)
                {
                    // AcquireTokenOnBehalfOf returned null
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, null, [connectionName, string.Join(",", scopes)]);
                }
            }
            catch (Exception)
            {
                await SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
                throw;
            }

            return token ?? null;
        }

        // TODO: this is the same code as in AzureBotUserAuthorization.  Refactor.
        private bool TryGetOBOProvider(string connectionName, out IOBOExchange oboExchangeProvider)
        {
            if (_connections.TryGetConnection(connectionName, out var tokenProvider))
            {
                if (tokenProvider is IOBOExchange oboExchange)
                {
                    oboExchangeProvider = oboExchange;
                    return true;
                }
            }

            oboExchangeProvider = null;
            return false;
        }
    }
}

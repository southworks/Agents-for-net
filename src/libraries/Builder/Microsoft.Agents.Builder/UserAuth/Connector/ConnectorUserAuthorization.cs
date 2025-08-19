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
            if (turnContext.Identity is CaseSensitiveClaimsIdentity identity)
            {
                // mock up the typical TokenResponse
                var tokenResponse = new TokenResponse()
                {
                    Token = identity.SecurityToken.ToString(),
                    IsExchangeable = true

                    // Probably don't need expiration for this.  Would need to extract from JWT token if so.
                    //Expiration = 
                };

                return await HandleOBO(turnContext, tokenResponse, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
            }

            throw new InvalidOperationException();
        }

        public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task<TokenResponse> SignInUserAsync(ITurnContext context, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            // TBD: This only works for authenticated claimsidentity
            if (context.Identity is CaseSensitiveClaimsIdentity identity)
            {
                // mock up the typical TokenResponse
                var tokenResponse = new TokenResponse()
                {
                    Token = identity.SecurityToken.ToString(),
                    IsExchangeable = true

                    // Probably don't need expiration for this.  Would need to extract from JWT token if so.
                    //Expiration = 
                };

                return await HandleOBO(context, tokenResponse, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
            }

            // TODO: define error
            throw new InvalidOperationException();
        }

        public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

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
    }
}

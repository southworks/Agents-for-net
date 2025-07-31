// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A365
{
    /// <summary>
    /// Handles OAuth using the Azure Bot Token Service.
    /// </summary>
    public class A365Authorization : IUserAuthorization
    {
        private readonly A365Extension _a365;
        private readonly A365AuthSettings _a365AuthSettings;

        /// <summary>
        /// Required constructor for type loader construction.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="storage"></param>
        /// <param name="connections"></param>
        /// <param name="configurationSection"></param>
        public A365Authorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection)
            : this(name, storage, connections, configurationSection.Get<A365AuthSettings>())
        {

        }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="storage">The storage to use.</param>
        /// <param name="connections"></param>
        public A365Authorization(string name, IStorage storage, IConnections connections, A365AuthSettings settings) 
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));

            AssertionHelpers.ThrowIfNull(connections, nameof(connections));
            _a365 = new A365Extension(connections);

            _a365AuthSettings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            var token = await _a365.GetAgentUserTokenAsync(turnContext, exchangeScopes ?? _a365AuthSettings.Scopes, cancellationToken).ConfigureAwait(false);
            return new TokenResponse(token: token);
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            var token = await _a365.GetAgentUserTokenAsync(turnContext, exchangeScopes ?? _a365AuthSettings.Scopes, cancellationToken).ConfigureAwait(false);
            return new TokenResponse(token: token);
        }

        /// <inheritdoc/>
        public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder.Errors;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.UserAuth
{
    /// <summary>
    /// Loads and dispatches request to a named instance of IUserAuthorization.
    /// </summary>
    /// <remarks>
    /// This utilizes type loading to support extensibility.
    /// </remarks>
    internal class UserAuthorizationDispatcher : IUserAuthorizationDispatcher
    {
        private readonly Dictionary<string, UserAuthorizationDefinition> _userAuthHandlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<UserAuthorizationDispatcher> _logger;
        private readonly IStorage _storage;
        private readonly IConnections _connections;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connections"></param>
        /// <param name="userAuthHandlers"></param>
        public UserAuthorizationDispatcher(IConnections connections, params IUserAuthorization[] userAuthHandlers)
        {
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));

            if (userAuthHandlers == null || userAuthHandlers.Length == 0)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NoUserAuthorizationHandlers, null);
            }

            foreach(var authenticator in userAuthHandlers)
            {
                _userAuthHandlers.Add(authenticator.Name, new UserAuthorizationDefinition() {  Instance = authenticator });
            }
        }

        /// <summary>
        /// Create dispatcher from config.
        /// </summary>
        /// <code>
        /// "UserAuthorization": {
        ///   "graph": {
        ///     "Assembly": null,  // Optional, defaults to OAuthAuthentication Assembly
        ///     "Type": null,      // Optional, defaults to OAuthAuthentication Type
        ///     "Settings": {      // Settings are Type specific, for OAuthAuthentication, any OAuthSettings property.
        ///     }
        ///   }
        /// }
        /// </code>
        /// <remarks>
        /// <see cref="OAuthAuthentication"/> is the default <see cref="IUserAuthorization"/> unless type loading is specified.  The `Settings`
        /// node for the defaults is properties in <see cref="OAuthSettings"/>.  This User Authentication is performed with
        /// the Azure Bot Token Service using `OAuth Connections` defined on the Azure Bot.
        /// </remarks>
        /// <param name="sp"></param>
        /// <param name="configuration"></param>
        /// <param name="storage"></param>
        /// <param name="configKey"></param>
        public UserAuthorizationDispatcher(IServiceProvider sp, IConfiguration configuration, IStorage storage, string configKey = "UserAuthentication")
        {
            _logger = (ILogger<UserAuthorizationDispatcher>)sp.GetService(typeof(ILogger<UserAuthorizationDispatcher>));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _connections = sp.GetService<IConnections>();

            var configDict = configuration.GetSection(configKey).Get<Dictionary<string, UserAuthorizationDefinition>>();
            _userAuthHandlers = new(configDict, StringComparer.OrdinalIgnoreCase);

            if (_userAuthHandlers.Count == 0)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NoUserAuthorizationHandlers, null);
            }

            var assemblyLoader = new UserAuthorizationModuleLoader(AssemblyLoadContext.Default, _logger);

            foreach (var definition in _userAuthHandlers)
            {
                definition.Value.Constructor = assemblyLoader.GetProviderConstructor(definition.Key, definition.Value.Assembly, definition.Value.Type);
            }
        }

        /// <inheritdoc/>
        public IUserAuthorization Default => _userAuthHandlers.Count > 0 
            ? GetHandlerInstance(_userAuthHandlers.First().Key) 
            : throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationHandlerNotFound, null);

        /// <inheritdoc/>
        public async Task<SignInResponse> SignUserInAsync(ITurnContext turnContext, string handlerName, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(nameof(turnContext));

            IUserAuthorization auth = Get(handlerName);
            string token;
            try
            {
                token = await auth.SignInUserAsync(turnContext, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SignInResponse newResponse = new(SignInStatus.Error)
                {
                    Error = ex,
                    Cause = AuthExceptionReason.Other
                };
                if (ex is AuthException authEx)
                {
                    newResponse.Cause = authEx.Cause;
                }

                return newResponse;
            }

            if (!string.IsNullOrEmpty(token))
            {
                return new SignInResponse(SignInStatus.Complete)
                {
                    Token = token
                };
            }

            return new SignInResponse(SignInStatus.Pending);
        }

        /// <inheritdoc/>
        public async Task SignOutUserAsync(ITurnContext turnContext, string handlerName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(nameof(turnContext));

            IUserAuthorization auth = Get(handlerName);
            await auth.SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ResetStateAsync(ITurnContext turnContext, string handlerName, CancellationToken cancellationToken = default)
        {
            await Get(handlerName).ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        public IUserAuthorization Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Default;
            }

            return GetHandlerInstance(name);
        }

        private IUserAuthorization GetHandlerInstance(string name)
        {
            if (!_userAuthHandlers.TryGetValue(name, out UserAuthorizationDefinition handlerDefinition))
            {
                throw ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.UserAuthorizationHandlerNotFound, null, name);
            }
            return GetHandlerInstance(name, handlerDefinition);
        }

        private IUserAuthorization GetHandlerInstance(string name, UserAuthorizationDefinition handlerDefinition)
        {
            if (handlerDefinition.Instance != null)
            {
                // Return existing instance.
                return handlerDefinition.Instance;
            }

            try
            {
                // Construct the provider
                handlerDefinition.Instance = handlerDefinition.Constructor.Invoke([name, _storage, _connections, handlerDefinition.Settings]) as IUserAuthorization;
                return handlerDefinition.Instance;
            }
            catch (Exception ex)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateUserAuthorizationHandler, ex, handlerDefinition.Type);
            }
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Errors;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
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
    /// Loads and dispatches request to a named instance of IUserAuthentication.
    /// </summary>
    /// <remarks>
    /// This utilizes type loading to support extensibility.
    /// </remarks>
    public class UserAuthenticationDispatcher : IUserAuthenticationDispatcher
    {
        private readonly Dictionary<string, UserAuthenticationDefinition> _userAuthHandlers = [];
        private readonly ILogger<UserAuthenticationDispatcher> _logger;
        private readonly IStorage _storage;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userAuthHandlers"></param>
        public UserAuthenticationDispatcher(params IUserAuthentication[] userAuthHandlers)
        {
            if (userAuthHandlers == null || userAuthHandlers.Length == 0)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NoUserAuthenticationHandlers, null);
            }

            foreach(var authenticator in userAuthHandlers)
            {
                _userAuthHandlers.Add(authenticator.Name, new UserAuthenticationDefinition() {  Instance = authenticator });
            }
        }

        /// <summary>
        /// Create dispatcher from config.
        /// </summary>
        /// <code>
        /// "UserAuthentication": {
        ///   "graph": {
        ///     "Assembly": null,  // Optional, defaults to OAuthAuthentication Assembly
        ///     "Type": null,      // Optional, defaults to OAuthAuthentication Type
        ///     "Settings": {      // Settings are Type specific, for OAuthAuthentication, any OAuthSettings property.
        ///     }
        ///   }
        /// }
        /// </code>
        /// <remarks>
        /// <see cref="OAuthAuthentication"/> is the default IUserAuthentication unless type loading is specified.  The `Settings`
        /// node for the defaults is properties in <see cref="OAuthSettings"/>.  This User Authentication is performed with
        /// the Azure Bot Token Service using `OAuth Connections` defined on the Azure Bot.
        /// </remarks>
        /// <param name="sp"></param>
        /// <param name="configuration"></param>
        /// <param name="storage"></param>
        /// <param name="configKey"></param>
        public UserAuthenticationDispatcher(IServiceProvider sp, IConfiguration configuration, IStorage storage, string configKey = "UserAuthentication")
        {
            _logger = (ILogger<UserAuthenticationDispatcher>)sp.GetService(typeof(ILogger<UserAuthenticationDispatcher>));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));

            _userAuthHandlers = configuration.GetSection(configKey).Get<Dictionary<string, UserAuthenticationDefinition>>() ?? [];
            if (_userAuthHandlers.Count == 0)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NoUserAuthenticationHandlers, null);
            }

            var assemblyLoader = new UserAuthenticationModuleLoader(AssemblyLoadContext.Default, _logger);

            foreach (var definition in _userAuthHandlers)
            {
                definition.Value.Constructor = assemblyLoader.GetProviderConstructor(definition.Key, definition.Value.Assembly, definition.Value.Type);
            }
        }

        public IUserAuthentication Default => _userAuthHandlers.Count > 0 
            ? GetHandlerInstance(_userAuthHandlers.First().Key) 
            : throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthenticationHandlerNotFound, null);

        /// <inheritdoc/>
        public async Task<SignInResponse> SignUserInAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(nameof(turnContext));

            IUserAuthentication auth = Get(flowName);
            TokenResponse token;
            try
            {
                token = await auth.SignInUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
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

            if (token != null)
            {
                return new SignInResponse(SignInStatus.Complete)
                {
                    TokenResponse = token
                };
            }

            return new SignInResponse(SignInStatus.Pending);
        }

        /// <inheritdoc/>
        public async Task SignOutUserAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(nameof(turnContext));

            IUserAuthentication auth = Get(flowName);
            await auth.SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ResetStateAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default)
        {
            await Get(flowName).ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        public IUserAuthentication Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Default;
            }

            return GetHandlerInstance(name);
        }

        private IUserAuthentication GetHandlerInstance(string name)
        {
            if (!_userAuthHandlers.TryGetValue(name, out UserAuthenticationDefinition handlerDefinition))
            {
                throw ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.UserAuthenticationHandlerNotFound, null, name);
            }
            return GetHandlerInstance(name, handlerDefinition);
        }

        private IUserAuthentication GetHandlerInstance(string name, UserAuthenticationDefinition handlerDefinition)
        {
            if (handlerDefinition.Instance != null)
            {
                // Return existing instance.
                return handlerDefinition.Instance;
            }

            try
            {
                // Construct the provider
                handlerDefinition.Instance = handlerDefinition.Constructor.Invoke([name, _storage, handlerDefinition.Settings]) as IUserAuthentication;
                return handlerDefinition.Instance;
            }
            catch (Exception ex)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateUserAuthenticationHandler, ex, handlerDefinition.Type);
            }
        }
    }
}

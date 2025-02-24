// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App.Authentication.TokenService;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.App.Authentication
{
    /// <summary>
    /// The user authentication manager
    /// </summary>
    public class AuthenticationManager
    {
        public delegate Task<bool> SignInCompletionHandlerAsync(ITurnContext turnContext, ITurnState turnState, string settingName, SignInResponse completionResponse, CancellationToken cancellationToken);

        private Dictionary<string, IAuthentication> _authentications;

        /// <summary>
        /// The default authentication setting name.
        /// </summary>
        public string Default { get; }

        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="options">The authentication options</param>
        /// <param name="storage">The storage to use.</param>
        /// <exception cref="TeamsAIException">Throws when the options does not contain authentication handlers</exception>
        public AuthenticationManager(Application app, AuthenticationOptions options)
        {
            if (options._authenticationSettings.Count == 0)
            {
                throw new ArgumentException("Authentications setting is empty");
            }

            _authentications = [];

            // If developer does not specify default authentication, set default to the first one in the options
            Default = options.Default ?? options._authenticationSettings.First().Key;

            foreach (string key in options._authenticationSettings.Keys)
            {
                object setting = options._authenticationSettings[key];
                if (setting is OAuthSettings oauthSetting)
                {
                    _authentications.Add(key, new OAuthAuthentication(key, oauthSetting, options.Storage));
                }
                //else if (setting is TeamsSsoSettings teamsSsoSettings)
                //{
                //    _authentications.Add(key, new TeamsSsoAuthentication(app, key, teamsSsoSettings, storage));
                //}
            }
        }

        /// <summary>
        /// Sign in a user.
        /// </summary>
        /// <remarks>
        /// On success, this will put the token in ITurnState.Temp.AuthTokens[settingName].
        /// </remarks>
        /// <param name="context">The turn context</param>
        /// <param name="settingName">The name of the authentication handler to use. If null, the default handler name is used.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in status</returns>
        public async Task<SignInResponse> SignUserInAsync(ITurnContext context, string settingName, CancellationToken cancellationToken = default)
        {
            settingName = settingName ?? Default;

            IAuthentication auth = Get(settingName);
            TokenResponse token;
            try
            {
                token = await auth.SignInUserAsync(context, cancellationToken).ConfigureAwait(false);
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

        /// <summary>
        /// Signs out a user.
        /// </summary>
        /// <param name="context">The turn context</param>
        /// <param name="state">The turn state</param>
        /// <param name="settingName">Optional. The name of the authentication handler to use. If not specified, the default handler name is used.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task SignOutUserAsync(ITurnContext context, ITurnState state, string? settingName = null, CancellationToken cancellationToken = default)
        {
            if (settingName == null)
            {
                settingName = Default;
            }

            IAuthentication auth = Get(settingName);
            await auth.SignOutUserAsync(context, cancellationToken).ConfigureAwait(false);
            AuthUtilities.DeleteTokenFromState(state, settingName);
        }

        /// <summary>
        /// Get an authentication class via name
        /// </summary>
        /// <param name="name">The name of authentication class</param>
        /// <returns>The authentication class</returns>
        /// <exception cref="InvalidOperationException">When cannot find the class with given name</exception>
        public IAuthentication Get(string name)
        {
            if (_authentications.ContainsKey(name))
            {
                return _authentications[name];
            }

            throw new InvalidOperationException($"Could not find authentication handler with name '{name}'.");
        }
    }
}

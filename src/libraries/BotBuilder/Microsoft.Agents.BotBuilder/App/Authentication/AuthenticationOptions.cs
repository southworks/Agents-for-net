// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App.Authentication.TokenService;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.App.Authentication
{
    /// <summary>
    /// Function for determining whether authentication should be enabled for an activity.
    /// </summary>
    /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>True if authentication should be enabled. Otherwise, False.</returns>
    public delegate Task<bool> SelectorAsync(ITurnContext turnContext, CancellationToken cancellationToken);

    /// <summary>
    /// Options for authentication.
    /// </summary>
    public class AuthenticationOptions
    {
        /// <summary>
        /// The authentication settings to sign-in and sign-out users.
        /// Key uniquely identifies each authentication.
        /// </summary>
        internal Dictionary<string, object> _authenticationSettings { get; set; }

        /// <summary>
        /// Describes the authentication class the bot should use if the user does not specify a authentication class name.
        /// If the value is not provided, the first one in `Authentications` setting will be used as the default one.
        /// </summary>
        public string? Default { get; set; }

        /// <summary>
        /// The IStorage used by all IAuthentication instances.
        /// </summary>
        public IStorage Storage { get; set; }

        /// <summary>
        /// Indicates whether the bot should start the sign in flow when the user sends a message to the bot or triggers a message extension.
        /// If the selector returns false, the bot will not start the sign in flow before routing the activity to the bot logic.
        /// If the selector is not provided, the sign in will always happen for valid activities.
        /// </summary>
        public SelectorAsync? AutoSignIn { get; set; }

        public Func<string, SignInResponse, IActivity[]> CompletedMessage { get; set; }

        public Func<string, SignInResponse, IActivity[]> SignInFailedMessage { get; set; } = (string flowName, SignInResponse response) => 
            [MessageFactory.Text(string.Format("Sign in for '{0}' completed without a token. Status={1}", flowName, response.Cause))];

        /// <summary>
        /// Configures the options to add an OAuth authentication setting.
        /// </summary>
        /// <param name="name">The authentication name.</param>
        /// <param name="oauthSettings">The OAuth settings</param>
        /// <returns>The object for chaining purposes.</returns>
        public AuthenticationOptions AddAuthentication(string name, OAuthSettings oauthSettings)
        {
            _authenticationSettings.Add(name, oauthSettings);
            return this;
        }

        /// <summary>
        /// The authentication options constructor.
        /// </summary>
        public AuthenticationOptions()
        {
            _authenticationSettings = [];
        }
    }
}

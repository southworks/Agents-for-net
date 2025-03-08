// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.App.UserAuth
{
    /// <summary>
    /// Function for determining whether authentication should be enabled for an activity.
    /// </summary>
    /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>True if authentication should be enabled. Otherwise, False.</returns>
    public delegate Task<bool> AutoSignInSelectorAsync(ITurnContext turnContext, CancellationToken cancellationToken);

    /// <summary>
    /// Options for authentication.
    /// </summary>
    public class UserAuthenticationOptions
    {
        public readonly static AutoSignInSelectorAsync AutoSignInOn = (context, cancellationToken) => Task.FromResult(true);
        public readonly static AutoSignInSelectorAsync AutoSignInOff = (context, cancellationToken) => Task.FromResult(UserAuthenticationFeature.IsSignInCompletionEvent(context.Activity));

        /// <summary>
        /// Creates UserAuthenticationOptions from IConfiguration and DI.
        /// </summary>
        /// <code>
        /// "UserAuthentication": {
        ///   "Default": "graph",
        ///   "AutoSignIn": true,
        ///   "Assembly": null,    // Optional
        ///   "Type": null,        // Optional, defaults to OAuthAuthentication
        ///   "Handlers": {
        ///     "graph": {
        ///     "Settings": {      // Settings are IUserAuthentication specific
        ///     }
        ///   }
        /// }
        /// </code>
        /// <remarks>
        /// The "AuthSignIn" property will map to <see cref="AutoSignInOn"/> or <see cref="AutoSignInOff"/>.  To provide a
        /// a custom selector, DI a <see cref="AutoSignInSelectorAsync"/>.
        /// </remarks>
        /// <remarks>
        /// The "Handlers" property is mapped to <see cref="UserAuthenticationDispatcher"/> using the key of `UserAuthentication:Handlers.  
        /// To provide a custom <see cref="IUserAuthenticationDispatcher"/> use DI.  If a custom IUserAuthenticationDispatcher is provided
        /// the Handlers node is note used.
        /// </remarks>
        /// <param name="sp"></param>
        /// <param name="configuration"></param>
        /// <param name="storage"></param>
        /// <param name="dispatcher"></param>
        /// <param name="autoSignInSelector"></param>
        /// <param name="configKey"></param>
        public UserAuthenticationOptions(
            IServiceProvider sp, 
            IConfiguration configuration, 
            IStorage storage = null,
            IUserAuthenticationDispatcher dispatcher = null, 
            AutoSignInSelectorAsync autoSignInSelector = null, 
            string configKey = "UserAuthentication")
        {
            var section = configuration.GetSection(configKey);
            Default = section.GetValue<string>(nameof(Default));
            Dispatcher = dispatcher ?? new UserAuthenticationDispatcher(sp, configuration, storage ?? sp.GetService<IStorage>(), configKey: $"{configKey}:Handlers");

            var selectorInstance = autoSignInSelector ?? sp.GetService<AutoSignInSelectorAsync>();
            var autoSignIn = section.GetValue<bool>(nameof(AutoSignIn), true);
            AutoSignIn = selectorInstance ?? (autoSignIn ? AutoSignInOn : AutoSignInOff);
        }

        public UserAuthenticationOptions(params IUserAuthentication[] userAuthHandlers)
        {
            Dispatcher = new UserAuthenticationDispatcher(userAuthHandlers);
        }

        /// <summary>
        /// The IUserAuthentication handlers.
        /// </summary>
        public IUserAuthenticationDispatcher Dispatcher { get; set; }

        /// <summary>
        /// Describes the authentication class the bot should use if the user does not specify a authentication class name.
        /// If the value is not provided, the first one in `Authentications` setting will be used as the default one.
        /// </summary>
        public string Default { get; set; }

        /// <summary>
        /// Indicates whether the bot should start the sign in flow when the user sends a message to the bot or triggers a message extension.
        /// If the selector returns false, the bot will not start the sign in flow before routing the activity to the bot logic.
        /// If the selector is not provided, the sign in will always happen for valid activities.
        /// </summary>
        public AutoSignInSelectorAsync? AutoSignIn { get; set; }

        /// <summary>
        /// Optional sign in completion message.  This is only used if the <see cref="UserAuthenticationFeature.OnUserSignInSuccess"/> is not set.
        /// </summary>
        public Func<string, SignInResponse, IActivity[]> CompletedMessage { get; set; }

        /// <summary>
        /// Optional sign in failure message.  This is only used if the <see cref="UserAuthenticationFeature.OnUserSignInFailure"/> is not set.
        /// </summary>
        public Func<string, SignInResponse, IActivity[]> SignInFailedMessage { get; set; } = 
            (flowName, response) => [MessageFactory.Text(string.Format("Sign in for '{0}' completed without a token. Status={1}", flowName, response.Cause))];
    }
}

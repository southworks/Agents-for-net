// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.BotBuilder.App.Authentication
{
    /// <summary>
    /// Authentication utilities
    /// </summary>
    internal class AuthUtilities
    {
        private const string IS_SIGNED_IN_KEY = "__InSignInFlow__";
        private const string SIGNIN_ACTIVITY_KEY = "__SignInFlowActivity__";

        public static string GetTokenInState(ITurnState turnState, string name)
        {
            if (turnState.Temp.AuthTokens.TryGetValue(name, out var token))
            {
                return token;
            }
            return null;
        }

        /// <summary>
        /// Set token in state
        /// </summary>
        /// <param name="state">The turn state</param>
        /// <param name="name">The name of token</param>
        /// <param name="token">The value of token</param>
        public static void SetTokenInState(ITurnState state, string name, string token)
        {
            state.Temp.AuthTokens[name] = token;
        }

        /// <summary>
        /// Delete token from turn state
        /// </summary>
        /// <param name="turnState">The turn state</param>
        /// <param name="name">The name of token</param>
        public static void DeleteTokenFromState(ITurnState turnState, string name)
        {
            if (turnState.Temp.AuthTokens != null && turnState.Temp.AuthTokens.ContainsKey(name))
            {
                turnState.Temp.AuthTokens.Remove(name);
            }
        }

        /// <summary>
        /// Determines if the user is in the sign in flow.
        /// </summary>
        /// <param name="turnState">The turn state.</param>
        /// <returns>The connection setting name if the user is in sign in flow. Otherwise null.</returns>
        public static string? UserInSignInFlow(ITurnState turnState)
        {
            string? value = turnState.User.GetValue<string>(IS_SIGNED_IN_KEY);

            if (value == string.Empty || value == null)
            {
                return null;
            }

            return value;
        }

        /// <summary>
        /// Update the turn state to indicate the user is in the sign in flow by providing the authentication setting name used.
        /// </summary>
        /// <param name="turnState">The turn state.</param>
        /// <param name="settingName">The connection setting name defined when configuring the authentication options within the application class.</param>
        public static void SetUserInSignInFlow(ITurnState turnState, string settingName)
        {
            turnState.User.SetValue(IS_SIGNED_IN_KEY, settingName);
        }

        /// <summary>
        /// Delete the user in sign in flow state from the turn state.
        /// </summary>
        /// <param name="turnState">The turn state.</param>
        public static void DeleteUserInSignInFlow(ITurnState turnState)
        {
            turnState.User.DeleteValue(IS_SIGNED_IN_KEY);
        }

        public static void SetUserSigninActivity(ITurnContext turnContext, ITurnState turnState)
        {
            turnState.User.SetValue(SIGNIN_ACTIVITY_KEY, turnContext.Activity);
        }

        public static IActivity DeleteUserSigninActivity(ITurnState turnState)
        {
            var activity = turnState.User.GetValue<IActivity>(SIGNIN_ACTIVITY_KEY);
            if (activity != null)
            {
                turnState.User.DeleteValue(SIGNIN_ACTIVITY_KEY);
            }
            return activity;
        }
    }
}

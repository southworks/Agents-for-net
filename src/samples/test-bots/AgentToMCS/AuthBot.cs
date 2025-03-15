// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.UserAuth;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AuthenticationBot
{
    public class AuthBot : AgentApplication
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private ConnectionSettings _mcsSettings = new ConnectionSettings()
        {
            EnvironmentId = "Default-69e9b82d-4842-4902-8d1e-abc5b98a55e8",
            BotIdentifier = "cref7_agent",
            CopilotBotType = BotType.Published,
            Cloud = PowerPlatformCloud.Prod,
        };

        public AuthBot(AgentApplicationOptions options, IHttpClientFactory clientFactory) : base(options)
        {
            _httpClientFactory = clientFactory;

            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

            OnMessage("/signin", SignInAsync);
            OnMessage(new Regex("/signout*."), SignOutAsync);
            OnMessage("/reset", ResetAsync);
            OnMessage("/obo", OBOSignInAsync);
            Authorization.OnUserSignInSuccess(OnUserSignInSuccess);
            Authorization.OnUserSignInFailure(OnUserSignInFailure);

            OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
        }

        private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to AuthenticationBot. Type 'auto' to demonstrate Auto SignIn. Type '/signin' to sign in for graph.  Type '/signout' to sign-out.  Anything else will be repeated back."), cancellationToken);
                }
            }
        }

        private async Task SignInAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await Authorization.SignInUserAsync(turnContext, turnState, "graph", cancellationToken: cancellationToken);
        }

        private async Task SignOutAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var split = turnContext.Activity.Text.Split(' ');

            if (split.Length == 1)
            {
                await Authorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
                await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
            }
            else
            {
                var handlerName = split[1].Trim();
                await Authorization.SignOutUserAsync(turnContext, turnState, handlerName, cancellationToken: cancellationToken);
                await turnContext.SendActivityAsync($"You have signed out from {handlerName}", cancellationToken: cancellationToken);
            }
        }

        private async Task ResetAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
            await turnState.User.DeleteStateAsync(turnContext, cancellationToken);
            await turnContext.SendActivityAsync("Ok I've deleted the current turn state", cancellationToken: cancellationToken);
        }

        private async Task OBOSignInAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await Authorization.SignInUserAsync(turnContext, turnState, "obo", exchangeScopes: [CopilotClient.ScopeFromSettings(_mcsSettings)], cancellationToken: cancellationToken);
        }

        private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text == "auto")
            {
                await turnContext.SendActivityAsync($"Auto Sign In: Successfully logged in to '{Authorization.Default}', token length: {Authorization.GetTurnToken(Authorization.Default).Length}", cancellationToken: cancellationToken);
            }
            else
            {
                // Not one of the defined inputs.  Just repeat what user said.
                await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
            }
        }

        private async Task OnUserSignInSuccess(ITurnContext turnContext, ITurnState turnState, string handlerName, string token, IActivity initiatingActivity, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync($"Manual Sign In: Successfully logged in to '{handlerName}'", cancellationToken: cancellationToken);
            if (handlerName == "obo")
            {
                CopilotClient cpsClient = new CopilotClient(
                    _mcsSettings,
                    _httpClientFactory,
                    tokenProviderFunction: (s) =>
                    {
                        return Task.FromResult(token);
                    },
                    NullLogger.Instance,
                    "mcs");

                await foreach (Activity act in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
                {
                    if (act is null)
                    {
                        throw new InvalidOperationException("Activity is null");
                    }
                }

            }
        }

        private async Task OnUserSignInFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync($"Manual Sign In: Failed to login to '{handlerName}': {response.Error.Message}", cancellationToken: cancellationToken);
        }
    }
}

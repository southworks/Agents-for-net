// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;

namespace Bot1
{
    /// <summary>
    /// Sample Agent calling another Agent.
    /// </summary>
    public class HostBot : AgentApplication
    {
        private readonly IChannelHost _channelHost;
        
        private const string Bot2Alias = "EchoBot";

        public HostBot(AgentApplicationOptions options, IChannelHost channelHost) : base(options)
        {
            _channelHost = channelHost ?? throw new ArgumentNullException(nameof(channelHost));
            BotResponses.OnBotReply(this, OnBotResponseAsync);

            // Add Activity routes
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
            OnActivity(ActivityTypes.EndOfConversation, OnEndOfConversationActivityAsync);
            OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

            OnTurnError = BotTurnErrorHandlerAsync;
        }

        private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
                }
            }
        }

        private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var echoBotConversation = _channelHost.GetExistingConversation(Bot2Alias, turnState);
            if (echoBotConversation != null)
            {
                // Already talking with Bot2.  Forward whatever the user says to Bot2.
                await _channelHost.SendToChannel(Bot2Alias, echoBotConversation, turnContext.Activity, cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("agent"))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Got it, connecting you to the agent..."), cancellationToken);

                // Create the ConversationId to use with Bot2.  This same conversationId should be used for all
                // subsequent SendToBot calls.  State is automatically saved after the turn is over.
                echoBotConversation = await _channelHost.GetOrCreateConversationAsync(Bot2Alias, turnState, turnContext.Identity, turnContext.Activity, cancellationToken);

                // Forward whatever the user said to Bot2.
                await _channelHost.SendToChannel(Bot2Alias, echoBotConversation, turnContext.Activity, cancellationToken);
            }
            else
            {
                // Respond with instructions
                await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
            }
        }

        // Handles response from Bot2.
        private async Task OnBotResponseAsync(ITurnContext turnContext, ITurnState turnState, BotConversationReference reference, IActivity botActivity, CancellationToken cancellationToken)
        {
            var echoBotConversation = _channelHost.GetExistingConversation(Bot2Alias, turnState);
            if (!string.Equals(echoBotConversation, botActivity.Conversation.Id, StringComparison.OrdinalIgnoreCase))
            {
                // This sample only deals with one active bot at a time.
                // We don't think we have an active conversation with this bot.  Ignore it.
                return;
            }
             
            if (string.Equals(ActivityTypes.EndOfConversation, botActivity.Type, StringComparison.OrdinalIgnoreCase))
            {
                // In this sample, the Bot2 will send an EndOfConversation with a result when "end" is sent.
                if (botActivity.Value != null)
                {
                    // Remove the channels conversation reference
                    await _channelHost.DeleteConversationAsync(echoBotConversation, turnState, cancellationToken);

                    var resultMessage = $"The channel returned:\n\n: {ProtocolJsonSerializer.ToJson(botActivity.Value)}";
                    await turnContext.SendActivityAsync(MessageFactory.Text(resultMessage), cancellationToken);
                }

                // Done with calling the remote Agent.
                await turnContext.SendActivityAsync(MessageFactory.Text("Back in the root bot. Say \"agent\" and I'll patch you through"), cancellationToken);
            }
            else
            {
                // Forward whatever the user sent to the channel until a result is returned.
                // Note that the botActivity is actually for a different conversation (contains a different ConversationReference).  It
                // cannot be sent directly to ABS without modification.  Here we are just extracting values we need.
                await turnContext.SendActivityAsync(MessageFactory.Text($"({Bot2Alias}) {botActivity.Text}"), cancellationToken);
            }
        }

        // Called either by the Channel-side sending EOC, or in the case of a TurnError.
        private async Task OnEndOfConversationActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var echoBotConversation = _channelHost.GetExistingConversation(Bot2Alias, turnState);
            if (echoBotConversation != null)
            {
                // Send EndOfConversation to Bot2.
                var eoc = Activity.CreateEndOfConversationActivity().ApplyConversationReference(turnContext.Activity.GetConversationReference());
                await _channelHost.SendToChannel(Bot2Alias, echoBotConversation, eoc, cancellationToken);

                // This conversation is over.
                await _channelHost.DeleteConversationAsync(echoBotConversation, turnState, cancellationToken);
            }
        }

        // This is the AgentApplication level OnTurnError handler.
        // Exceptions here will bubble-up to Adapter.OnTurnError.  If the exception isn't rethrown, then
        // the Turn will complete normally.
        private async Task BotTurnErrorHandlerAsync(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken)
        {
            await OnEndOfConversationActivityAsync(turnContext, turnState, cancellationToken);
            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken);

            var errorMessageText = "The bot encountered an error or bug.";
            var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.IgnoringInput.ToString());
            await turnContext.SendActivityAsync(errorMessage, CancellationToken.None);

            // Send a trace activity, which will be displayed in the Bot Framework Emulator
            await turnContext.TraceActivityAsync("OnTurnError Trace", exception.ToString(), "https://www.botframework.com/schemas/error", "TurnError", cancellationToken: cancellationToken);
        }
    }
}

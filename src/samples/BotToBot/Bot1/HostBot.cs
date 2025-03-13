// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Client;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.BotBuilder;
using System.Linq;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;

namespace Bot1
{
    /// <summary>
    /// Sample Agent calling another Agent.
    /// </summary>
    public class HostBot : AgentApplication
    {
        private readonly IConversationIdFactory _conversationIdFactory;
        private readonly IChannelHost _channelHost;

        private const string StateProperty = "conversation.state";
        private const string Bot2Alias = "EchoBot";

        public HostBot(AgentApplicationOptions options, IChannelHost channelHost, IConversationIdFactory conversationIdFactory) : base(options)
        {
            _channelHost = channelHost ?? throw new ArgumentNullException(nameof(channelHost));
            _conversationIdFactory = conversationIdFactory ?? throw new ArgumentNullException(nameof(conversationIdFactory));

            // Add Activity routes
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
            OnActivity(ActivityTypes.EndOfConversation, OnEndOfConversationActivityAsync);
            OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

            // BotResponses will be routed to OnBotResponse method.
            OnActivity(
                (turnContext, CancellationToken) => 
                    Task.FromResult(string.Equals(ActivityTypes.Event, turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(AdapterBotResponseHandler.BotResponseEventName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase)),
                OnBotResponse);

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
            // Get active conversationId being used for the other bot.  If null, a conversation hasn't been started.
            var state = turnState.GetValue<State>(StateProperty, () => new State());

            if (state.Bot2ConversationId != null)
            {
                // Already talking with Bot2.  Forward whatever the user says to Bot2.
                await SendToBot(state.Bot2ConversationId, _channelHost.Channels[Bot2Alias], turnContext.Activity, cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("agent"))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Got it, connecting you to the agent..."), cancellationToken);

                // Create the ConversationId to use with Bot2.  This same conversationId should be used for all
                // subsequent SendToBot calls.  State is automatically saved after the turn is over.
                state.Bot2ConversationId = await CreateConversationId(turnContext, Bot2Alias, cancellationToken);

                // Forward whatever the user said to Bot2.
                await SendToBot(state.Bot2ConversationId, _channelHost.Channels[Bot2Alias], turnContext.Activity, cancellationToken);
            }
            else
            {
                // Respond with instructions
                await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
            }
        }

        // Handles response from Bot2.
        // This is from the custom Event sent by AdapterBotResponseHandler.
        // turnContext.Activity is the Event.  It's Activity.Value is a AdapterBotResponseHandler.BotResponse, which
        // contains the Activity Bot2 sent, and the BotConversationReference.
        private async Task OnBotResponse(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            // Get active conversationId being used for the other bot.  If null, a conversation hasn't been started.
            var state = turnState.GetValue<State>(StateProperty, () => new State());
            var botResponse = ProtocolJsonSerializer.ToObject<AdapterBotResponseHandler.BotResponse>(turnContext.Activity.Value);

            if (!string.Equals(state.Bot2ConversationId, botResponse.Activity.Conversation.Id, StringComparison.OrdinalIgnoreCase))
            {
                // This sample only deals with one active bot at a time.
                // We don't think we have an active conversation with this bot.  Ignore it.
                return;
            }
             
            if (string.Equals(ActivityTypes.EndOfConversation, botResponse.Activity.Type, StringComparison.OrdinalIgnoreCase))
            {
                // In this sample, the Bot2 will send an EndOfConversation with a result when "end" is sent.
                if (botResponse.Activity.Value != null)
                {
                    var resultMessage = $"The channel returned:\n\n: {ProtocolJsonSerializer.ToJson(botResponse.Activity.Value)}";
                    await turnContext.SendActivityAsync(MessageFactory.Text(resultMessage), cancellationToken);

                    // Remove the channels conversation reference
                    await _conversationIdFactory.DeleteConversationReferenceAsync(state.Bot2ConversationId, cancellationToken);
                    turnState.DeleteValue(StateProperty);
                }

                // Done with calling the remote Agent.
                await turnContext.SendActivityAsync(MessageFactory.Text("Back in the root bot. Say \"agent\" and I'll patch you through"), cancellationToken);
            }
            else
            {
                // Forward whatever the user sent to the channel until a result is returned.
                await turnContext.SendActivityAsync(MessageFactory.Text($"({Bot2Alias}) {botResponse.Activity.Text}"), cancellationToken);
            }
        }

        private async Task<string> CreateConversationId(ITurnContext turnContext, string botAlias, CancellationToken cancellationToken)
        {
            // Create a new conversationId for the Bot2.  This conversationId should be used for all subsequent messages until a result is returned.
            var options = new ConversationIdFactoryOptions
            {
                FromBotOAuthScope = BotClaims.GetTokenScopes(turnContext.Identity)?.First(),
                FromBotId = _channelHost.HostAppId,
                Activity = turnContext.Activity,
                Bot = _channelHost.Channels[botAlias]
            };
            return await _conversationIdFactory.CreateConversationIdAsync(options, cancellationToken);
        }

        private async Task SendToBot(string targetBotConversationId, IChannelInfo targetChannel, IActivity activity, CancellationToken cancellationToken)
        {
            using var channel = _channelHost.GetChannel(targetChannel);

            // route the activity to the skill
            var response = await channel.PostActivityAsync(targetChannel.AppId, targetChannel.ResourceUrl, targetChannel.Endpoint, _channelHost.HostEndpoint, targetBotConversationId, activity, cancellationToken);

            // Check response status
            if (!(response.Status >= 200 && response.Status <= 299))
            {
                throw new HttpRequestException($"Error invoking the bot id: \"{targetChannel.Id}\" at \"{targetChannel.Endpoint}\" (status is {response.Status}). \r\n {response.Body}");
            }
        }

        // Called either by the Channel-side sending EOC, or in the case of a TurnError.
        private async Task OnEndOfConversationActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var state = turnState.GetValue<State>(StateProperty, () => new State());

            if (state.Bot2ConversationId != null)
            {
                // Send EndOfConversation to Bot2.
                var eoc = Activity.CreateEndOfConversationActivity().ApplyConversationReference(turnContext.Activity.GetConversationReference());
                await SendToBot(state.Bot2ConversationId, _channelHost.Channels[Bot2Alias], eoc, cancellationToken);

                // This conversation is over.
                await _conversationIdFactory.DeleteConversationReferenceAsync(state.Bot2ConversationId, cancellationToken);
                state.Bot2ConversationId = null;
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

            errorMessageText = "To continue to run this bot, please fix the bot source code.";
            errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput.ToString());
            await turnContext.SendActivityAsync(errorMessage, CancellationToken.None);

            // Send a trace activity, which will be displayed in the Bot Framework Emulator
            await turnContext.TraceActivityAsync("OnTurnError Trace", exception.ToString(), "https://www.botframework.com/schemas/error", "TurnError");
        }
    }

    class State
    {
        public string Bot2ConversationId { get; set; }
    }

}

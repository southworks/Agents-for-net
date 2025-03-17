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

namespace Bot1;

/// <summary>
/// Sample Agent calling another Agent.
/// </summary>
public class HostBot : AgentApplication
{
    // This provides access to Agent Channels operations.
    private readonly IChannelHost _channelHost;
    
    // The ChannelHost Channel this sample will communicate with.  This name matches
    // ChannelHost:Channels config.
    private const string Bot2Name = "EchoBot";

    public HostBot(AgentApplicationOptions options, IChannelHost channelHost) : base(options)
    {
        _channelHost = channelHost ?? throw new ArgumentNullException(nameof(channelHost));
        BotResponses.OnBotReply(this, OnBotResponseAsync);

        // Add Activity routes
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.EndOfConversation, OnEndOfConversationActivityAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

        OnTurnError = TurnErrorHandlerAsync;
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
        var echoBotConversationId = _channelHost.GetExistingConversation(turnContext, turnState.Conversation, Bot2Name);
        if (echoBotConversationId != null)
        {
            // Already talking with Bot2.  Forward whatever the user says to Bot2.
            await _channelHost.SendToChannel(Bot2Name, echoBotConversationId, turnContext.Activity, cancellationToken);
        }
        else if (turnContext.Activity.Text.Contains("agent"))
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Got it, connecting you to the agent..."), cancellationToken);

            // Create the Conversation to use with Bot2.  This same conversationId should be used for all
            // subsequent SendToBot calls.  State is automatically saved after the turn is over.
            echoBotConversationId = await _channelHost.GetOrCreateConversationAsync(turnContext, turnState.Conversation, Bot2Name, cancellationToken);

            // Forward whatever the user said to Bot2.
            await _channelHost.SendToChannel(Bot2Name, echoBotConversationId, turnContext.Activity, cancellationToken);
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
        var echoBotConversationId = _channelHost.GetExistingConversation(turnContext, turnState.Conversation, Bot2Name);
        if (!string.Equals(echoBotConversationId, botActivity.Conversation.Id, StringComparison.OrdinalIgnoreCase))
        {
            // This sample only deals with one active bot at a time.
            // We don't think we have an active conversation with this bot.  Ignore it.
            // For an AgentApplication that is handling replies from more that one channel, then the BotConversationReference.ChannelName
            // can be used to determine which Channel it's from. 
            return;
        }
         
        if (string.Equals(ActivityTypes.EndOfConversation, botActivity.Type, StringComparison.OrdinalIgnoreCase))
        {
            // In this sample, the Bot2 will send an EndOfConversation with a result when "end" is sent.
            if (botActivity.Value != null)
            {
                // Remove the channels conversation reference
                await _channelHost.DeleteConversationAsync(echoBotConversationId, turnState.Conversation, cancellationToken);

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
            await turnContext.SendActivityAsync(MessageFactory.Text($"({Bot2Name}) {botActivity.Text}"), cancellationToken);
        }
    }

    // Called either by the User sending EOC, or in the case of an AgentApplication TurnError.
    private async Task OnEndOfConversationActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // End all active channel conversations.
        var activeConversations = _channelHost.GetExistingConversations(turnContext, turnState.Conversation);
        if (activeConversations.Count > 0)
        {
            foreach (var conversation in activeConversations)
            {
                // Delete the ChannelHost conversation.
                await _channelHost.DeleteConversationAsync(conversation.ChannelConversationId, turnState.Conversation, cancellationToken);

                // Send EndOfConversation to Bot2.
                await _channelHost.SendToChannel(Bot2Name, conversation.ChannelConversationId, Activity.CreateEndOfConversationActivity(), cancellationToken);
            }
        }

        // No longer need this conversations state.
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
        await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken);
    }

    // This is the AgentApplication level OnTurnError handler.  See: AgentApplication.OnTurnError.
    private async Task TurnErrorHandlerAsync(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(MessageFactory.Text($"The bot encountered an error: {exception.Message}"), CancellationToken.None);

        // End any active channel conversations
        await OnEndOfConversationActivityAsync(turnContext, turnState, cancellationToken);
    }
}

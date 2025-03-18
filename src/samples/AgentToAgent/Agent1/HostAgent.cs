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

namespace Agent1;

/// <summary>
/// Sample Agent calling another Agent.
/// </summary>
public class HostAgent : AgentApplication
{
    // This provides access to other Agents.
    private readonly IAgentHost _agentHost;
    
    // The Agent this sample will communicate with.  This name matches AgentHost:Agents config.
    private const string Agent2Name = "Echo";

    public HostAgent(AgentApplicationOptions options, IAgentHost agentHost) : base(options)
    {
        _agentHost = agentHost ?? throw new ArgumentNullException(nameof(agentHost));
        AgentResponses.OnAgentReply(this, OnAgentResponseAsync);

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
        var echoConversationId = _agentHost.GetExistingConversation(turnContext, turnState.Conversation, Agent2Name);
        if (echoConversationId != null)
        {
            // Already talking with Agent2.  Forward whatever the user says to Agent2.
            await _agentHost.SendToAgent(Agent2Name, echoConversationId, turnContext.Activity, cancellationToken);
        }
        else if (turnContext.Activity.Text.Contains("agent"))
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Got it, connecting you to the agent..."), cancellationToken);

            // Create the Conversation to use with Agent2.  This same conversationId should be used for all
            // subsequent SendToBot calls.  State is automatically saved after the turn is over.
            echoConversationId = await _agentHost.GetOrCreateConversationAsync(turnContext, turnState.Conversation, Agent2Name, cancellationToken);

            // Forward whatever the user said to Agent2.
            await _agentHost.SendToAgent(Agent2Name, echoConversationId, turnContext.Activity, cancellationToken);
        }
        else
        {
            // Respond with instructions
            await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
        }
    }

    // Handles response from Agent2.
    private async Task OnAgentResponseAsync(ITurnContext turnContext, ITurnState turnState, ChannelConversationReference reference, IActivity channelActivity, CancellationToken cancellationToken)
    {
        var echoConversationId = _agentHost.GetExistingConversation(turnContext, turnState.Conversation, Agent2Name);
        if (!string.Equals(echoConversationId, channelActivity.Conversation.Id, StringComparison.OrdinalIgnoreCase))
        {
            // This sample only deals with one active Agent at a time.
            // We don't think we have an active conversation with this Agent.  Ignore it.
            // For an AgentApplication that is handling replies from more that one Agent channel, then the ChannelConversationReference.ChannelName
            // can be used to determine which Channel it's from. 
            return;
        }
         
        if (string.Equals(ActivityTypes.EndOfConversation, channelActivity.Type, StringComparison.OrdinalIgnoreCase))
        {
            // In this sample, the Agent2 will send an EndOfConversation with a result when "end" is sent.
            if (channelActivity.Value != null)
            {
                // Remove the channels conversation reference
                await _agentHost.DeleteConversationAsync(echoConversationId, turnState.Conversation, cancellationToken);

                var resultMessage = $"The {Agent2Name} Agent returned:\n\n: {ProtocolJsonSerializer.ToJson(channelActivity.Value)}";
                await turnContext.SendActivityAsync(MessageFactory.Text(resultMessage), cancellationToken);
            }

            // Done with calling the remote Agent.
            await turnContext.SendActivityAsync(MessageFactory.Text($"Back in {nameof(HostAgent)}. Say \"agent\" and I'll patch you through"), cancellationToken);
        }
        else
        {
            // Forward whatever the user sent to the channel until EndOfConversation is received from an Agent channel.
            // Note that the channelActivity is actually for a different conversation (contains a different ConversationReference).  It
            // cannot be sent directly to ABS without modification.  Here we are just extracting values we need.
            await turnContext.SendActivityAsync(MessageFactory.Text($"({reference.ChannelName}) {channelActivity.Text}"), cancellationToken);
        }
    }

    // Called either by the User sending EOC, or in the case of an AgentApplication TurnError.
    private async Task OnEndOfConversationActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // End all active channel conversations.
        var activeConversations = _agentHost.GetExistingConversations(turnContext, turnState.Conversation);
        if (activeConversations.Count > 0)
        {
            foreach (var conversation in activeConversations)
            {
                // Delete the conversation because we're done with it.
                await _agentHost.DeleteConversationAsync(conversation.ChannelConversationId, turnState.Conversation, cancellationToken);

                // Send EndOfConversation to the Agent.
                await _agentHost.SendToAgent(Agent2Name, conversation.ChannelConversationId, Activity.CreateEndOfConversationActivity(), cancellationToken);
            }
        }

        // No longer need this conversations state.
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
        await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken);
    }

    // This is the AgentApplication level OnTurnError handler.  See: AgentApplication.OnTurnError.
    private async Task TurnErrorHandlerAsync(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(MessageFactory.Text($"{nameof(HostAgent)} encountered an error: {exception.Message}"), CancellationToken.None);

        // End any active channel conversations
        await OnEndOfConversationActivityAsync(turnContext, turnState, cancellationToken);
    }
}

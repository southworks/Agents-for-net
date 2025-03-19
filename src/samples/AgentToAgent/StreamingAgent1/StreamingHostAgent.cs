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

namespace StreamingAgent1;

/// <summary>
/// Sample Agent calling another Agent.
/// </summary>
public class StreamingHostAgent : AgentApplication
{
    // This provides access to other Agents.
    private readonly IAgentHost _agentHost;
    
    // The Agent this sample will communicate with.  This name matches AgentHost:Agents config.
    private const string Agent2Name = "Echo";

    public StreamingHostAgent(AgentApplicationOptions options, IAgentHost agentHost) : base(options)
    {
        _agentHost = agentHost ?? throw new ArgumentNullException(nameof(agentHost));

        // Add an AgentApplication turn error handler.
        OnTurnError(TurnErrorHandlerAsync);
    }

    [Route(RouteType = RouteType.Conversation, EventName = ConversationUpdateEvents.MembersAdded)]
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

    [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Message, Rank = RouteRank.Last)]
    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var echoConversationId = _agentHost.GetExistingConversation(turnContext, turnState.Conversation, Agent2Name);

        if (echoConversationId == null)
        {
            if (turnContext.Activity.Text.Contains("agent"))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Got it, connecting you to the '{Agent2Name}' Agent..."), cancellationToken);

                // Create the Conversation to use with Agent2.  This same conversationId should be used for all
                // subsequent SendToBot calls.  State is automatically saved after the turn is over.
                echoConversationId = await _agentHost.GetOrCreateConversationAsync(turnContext, turnState.Conversation, Agent2Name, cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
                return;
            }
        }

        // Forward whatever the user says to Agent2.
        var channel = _agentHost.GetChannel(Agent2Name);

        // Forward whatever C2 sent to the channel until a result is returned.
        var result = await channel.SendActivityStreamedAsync<object>(
            echoConversationId,
            turnContext.Activity,
            async (activity) =>
            {
                // Just repeat message to C2
                await turnContext.SendActivityAsync(MessageFactory.Text($"({channel.Name}) {activity.Text}"), cancellationToken);
            },
            cancellationToken: cancellationToken);

        // SendActivityStreamedAsync completes when the Agent2 turn is over.  In this sample, SendActivityStreamedAsync will return
        // the result the Agent2 sent when "end" is received.
        if (result != null)
        {
            // Remove the channels conversation reference
            await _agentHost.DeleteConversationAsync(echoConversationId, turnState.Conversation, cancellationToken);

            var resultMessage = $"The '{Agent2Name}' Agent returned:\n\n: {ProtocolJsonSerializer.ToJson(result)}";
            await turnContext.SendActivityAsync(MessageFactory.Text(resultMessage), cancellationToken);

            // Done with calling the remote Agent.
            await turnContext.SendActivityAsync(MessageFactory.Text($"Back in {nameof(StreamingHostAgent)}. Say \"agent\" and I'll patch you through"), cancellationToken);
        }
    }

    // Called either by the User sending EOC, or in the case of an AgentApplication TurnError.
    [Route(RouteType = RouteType.Activity, Type = ActivityTypes.EndOfConversation)]
    private async Task OnEndOfConversationActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await _agentHost.EndAllActiveConversations(turnContext, turnState, cancellationToken);
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
    }

    private async Task TurnErrorHandlerAsync(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken)
    {
        await OnEndOfConversationActivityAsync(turnContext, turnState, cancellationToken);
    }
}

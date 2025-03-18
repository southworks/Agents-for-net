// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent2;

public class Echo : AgentApplication
{
    public Echo(AgentApplicationOptions options) : base(options)
    {
        // Add Activity routes
        OnActivity(ActivityTypes.EndOfConversation, OnEndOfConversationActivityAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

        OnTurnError = BotTurnErrorHandlerAsync;
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var result = turnState.Conversation.GetValue("log", () => new EchoResult());

        if (turnContext.Activity.Text.Contains("end"))
        {
            // Send End of conversation at the end.
            result.Messages.Add(turnContext.Activity.Text);
            await turnContext.SendActivityAsync(MessageFactory.Text("Ending conversation..."), cancellationToken);
            var endOfConversation = Activity.CreateEndOfConversationActivity();
            endOfConversation.Code = EndOfConversationCodes.CompletedSuccessfully;
            endOfConversation.Value = result;
            await turnContext.SendActivityAsync(endOfConversation, cancellationToken);

            // No longer need to keep state for this conversation.
            await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
        }
        else
        {
            result.Messages.Add(turnContext.Activity.Text);
            await turnContext.SendActivityAsync(MessageFactory.Text(turnContext.Activity.Text), cancellationToken);
            var messageText = "Say \"end\" and I'll end the conversation and return to the parent.";
            await turnContext.SendActivityAsync(MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput.ToString()), cancellationToken);
        }
    }

    private async Task OnEndOfConversationActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // This will be called if the root bot is ending the conversation.  Sending additional messages should be
        // avoided as the conversation may have been deleted.
        // Perform cleanup of resources if needed.
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
    }

    private async Task BotTurnErrorHandlerAsync(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken)
    {
        // Send a message to the user.
        var errorMessageText = "Bot2 encountered an error or bug.";
        var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.IgnoringInput.ToString());
        await turnContext.SendActivityAsync(errorMessage, CancellationToken.None);

        // Send an EndOfConversation activity to the caller with the error to end the conversation.
        var endOfConversation = Activity.CreateEndOfConversationActivity();
        endOfConversation.Code = "Error";
        endOfConversation.Text = exception.Message;
        await turnContext.SendActivityAsync(endOfConversation, CancellationToken.None);
    }
}

class EchoResult
{
    public List<string> Messages { get; set; } = [];
}

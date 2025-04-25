// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Extensions.Teams.App;

namespace HeaderPropagation;

public class Echo : AgentApplication
{
    public Echo(AgentApplicationOptions options) : base(options)
    {
        RegisterExtension(new TeamsAgentExtension(this), (ext) =>
        {
            ext.OnMessageEdit(OnMessageEditAsync);
        });
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task OnMessageEditAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await OnMessageAsync(turnContext, turnState, cancellationToken).ConfigureAwait(false);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Increment count state.
        int count = turnState.Conversation.IncrementMessageCount();

        await turnContext.SendActivityAsync($"[{count}] you said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}

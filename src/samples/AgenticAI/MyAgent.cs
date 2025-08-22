// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgenticAI.AgenticExtension;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Graph.Models;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI;

public class MyAgent : AgentApplication
{
    public AgentAuthorization AgentAuthorization { get; init; }

    public MyAgent(AgentApplicationOptions options, IConnections connections, IHttpClientFactory httpClientFactory) : base(options)
    {
        AgentAuthorization = new AgentAuthorization(connections, httpClientFactory);

        // Register a route for AgenticAI-only Messages
        OnActivity(AgentAuthorization.AgenticAIMessage, OnAgenticMessageAsync);
    }

    private async Task OnAgenticMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Create the chat message
        var chatMessage = new ChatMessage
        {
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = $"You said: {turnContext.Activity.Text}"
            }
        };

        var graphClient = AgentAuthorization.GraphClientForAgentUser(turnContext, ["https://canary.graph.microsoft.com/.default"]);
        // Send the message to the chat
        await graphClient.Chats[turnContext.Activity.Conversation.Id].Messages
            .PostAsync(chatMessage);
    }
}

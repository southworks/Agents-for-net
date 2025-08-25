// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgenticAI.AgenticExtension;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI;

public class MyAgent : AgentApplication
{
    // This will be provided in AgentApplication in some form for release.
    // For the demo, create it here with a local implementation.
    public AgentAuthorization AgentAuthorization { get; init; }

    public MyAgent(AgentApplicationOptions options, IConnections connections, IHttpClientFactory httpClientFactory) : base(options)
    {
        AgentAuthorization = new AgentAuthorization(connections, httpClientFactory);

        // Register a route for AgenticAI-only Messages.
        OnActivity(AgentAuthorization.AgenticAIMessage, OnAgenticMessageAsync);
    }

    private async Task OnAgenticMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}

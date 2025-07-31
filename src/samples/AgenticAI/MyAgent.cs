// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A365;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options, A365Extension a365Extension) : base(options)
    {
        RegisterExtension(a365Extension, a365 =>
        {
            a365.AgentApplication = this;  // Perhaps RegisterExtension should do this

            // Register a route for AgenticAI-only Messages.
            a365.OnActivity(ActivityTypes.Message, OnAgenticMessageAsync, autoSignInHandlers: ["agentic"]);
        });

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task OnAgenticMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var aauToken = await UserAuthorization.GetTurnTokenAsync(turnContext, "agentic", cancellationToken);
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}, user token len={aauToken.Length}", cancellationToken: cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}

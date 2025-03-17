using Microsoft.Agents.BotBuilder.App;

namespace Microsoft.Agents.BotBuilder;


/// <summary>
/// Represents an opinionated base class for Agent Extensions.
/// </summary>
public abstract class AgentExtension : IAgentExtension
{
    public virtual string ChannelId {get;init;}
    public void AddRoute(AgentApplication agentApplication, RouteSelectorAsync routeSelectorAsync, RouteHandler routeHandler, bool isInvokeRoute = false) {
        var ensureChannelMatches = new RouteSelectorAsync(async (turnContext, cancellationToken) => {
            return turnContext.Activity.ChannelId == ChannelId && await routeSelectorAsync(turnContext, cancellationToken);
        });

        agentApplication.AddRoute(ensureChannelMatches, routeHandler, isInvokeRoute);
    }
}

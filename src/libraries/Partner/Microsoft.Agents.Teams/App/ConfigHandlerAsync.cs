using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Teams.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Teams.App
{
    /// <summary>
    /// Function for handling config events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="configData">The config data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of ConfigResponseBase.</returns>
    public delegate Task<ConfigResponseBase> ConfigHandlerAsync(ITurnContext turnContext, ITurnState turnState, object configData, CancellationToken cancellationToken);
}

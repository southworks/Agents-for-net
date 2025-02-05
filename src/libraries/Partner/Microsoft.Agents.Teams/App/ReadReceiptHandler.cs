using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Teams.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Teams.App
{
    /// <summary>
    /// Function for handling read receipt events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The read receipt data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns></returns>
    public delegate Task ReadReceiptHandler(ITurnContext turnContext, ITurnState turnState, ReadReceiptInfo data, CancellationToken cancellationToken);
}

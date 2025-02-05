using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Teams.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Teams.App
{
    /// <summary>
    /// Function for handling file consent card activities.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="fileConsentCardResponse">The response representing the value of the invoke activity sent when the user acts on a file consent card.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task FileConsentHandler(ITurnContext turnContext, ITurnState turnState, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken);
}

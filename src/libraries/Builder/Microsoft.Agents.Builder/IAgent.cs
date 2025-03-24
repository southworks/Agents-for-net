// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Represents an Agent that can operate on incoming activities.
    /// </summary>
    /// <remarks>
    /// A <see cref="IChannelAdapter"/> passes incoming activities from the channel
    /// to the Agent's <see cref="OnTurnAsync(ITurnContext, CancellationToken)"/> method.
    /// </remarks>
    /// <seealso cref="AgentCallbackHandler"/>
    public interface IAgent
    {
        /// <summary>
        /// When implemented in an Agent, handles an incoming activity.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>The <paramref name="turnContext"/> provides information about the
        /// incoming activity, and other data needed to process the activity.</remarks>
        /// <seealso cref="ITurnContext"/>
        Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);
    }
}

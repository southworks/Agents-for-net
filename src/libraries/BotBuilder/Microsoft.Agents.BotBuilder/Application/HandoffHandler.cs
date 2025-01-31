// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Application.State;
using Microsoft.Agents.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.Application
{
    /// <summary>
    /// Function for handling handoff activities.
    /// </summary>
    /// <typeparam name="TState">Type of the turn state. This allows for strongly typed access to the turn state.</typeparam>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="continuation">The continuation token.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task HandoffHandler<TState>(ITurnContext turnContext, TState turnState, string continuation, CancellationToken cancellationToken) where TState : TurnState;
}

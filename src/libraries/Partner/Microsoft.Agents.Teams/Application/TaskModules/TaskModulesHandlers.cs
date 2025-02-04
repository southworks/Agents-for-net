﻿using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Application.State;
using Microsoft.Agents.Teams.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Teams.Application.TaskModules
{
    /// <summary>
    /// Function for handling Task Module fetch events.
    /// </summary>
    /// <typeparam name="TState">Type of the turn state. This allows for strongly typed access to the turn state.</typeparam>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The data associated with the fetch.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>An instance of TaskModuleResponse.</returns>
    public delegate Task<TaskModuleResponse> FetchHandlerAsync<TState>(ITurnContext turnContext, TState turnState, object data, CancellationToken cancellationToken) where TState : TurnState;

    /// <summary>
    /// Function for handling Task Module submit events.
    /// </summary>
    /// <typeparam name="TState">Type of the turn state. This allows for strongly typed access to the turn state.</typeparam>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The data associated with the fetch.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>An instance of TaskModuleResponse.</returns>
    public delegate Task<TaskModuleResponse> SubmitHandlerAsync<TState>(ITurnContext turnContext, TState turnState, object data, CancellationToken cancellationToken) where TState : TurnState;

}

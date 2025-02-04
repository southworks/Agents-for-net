﻿using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Application.State;
using Microsoft.Agents.Teams.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Teams.Application.Meetings
{
    /// <summary>
    /// Function for handling Microsoft Teams meeting start events.
    /// </summary>
    /// <typeparam name="TState">Type of the turn state. This allows for strongly typed access to the turn state.</typeparam>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="meeting">The details of the meeting.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task MeetingStartHandler<TState>(ITurnContext turnContext, TState turnState, MeetingStartEventDetails meeting, CancellationToken cancellationToken) where TState : TurnState;

    /// <summary>
    /// Function for handling Microsoft Teams meeting end events.
    /// </summary>
    /// <typeparam name="TState">Type of the turn state. This allows for strongly typed access to the turn state.</typeparam>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="meeting">The details of the meeting.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task MeetingEndHandler<TState>(ITurnContext turnContext, TState turnState, MeetingEndEventDetails meeting, CancellationToken cancellationToken) where TState : TurnState;

    /// <summary>
    /// Function for handling Microsoft Teams meeting participants join or leave events.
    /// </summary>
    /// <typeparam name="TState">Type of the turn state. This allows for strongly typed access to the turn state.</typeparam>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="meeting">The details of the meeting.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task MeetingParticipantsEventHandler<TState>(ITurnContext turnContext, TState turnState, MeetingParticipantsEventDetails meeting, CancellationToken cancellationToken) where TState : TurnState;

}

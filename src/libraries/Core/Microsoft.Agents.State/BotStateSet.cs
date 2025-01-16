﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Agents.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.State
{
    /// <summary>
    ///  Manages a collection of botState and provides ability to load and save in parallel.
    /// </summary>
    public class BotStateSet
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BotStateSet"/> class.
        /// </summary>
        /// <param name="botStates">initial list of <see cref="BotState"/> objects to manage.</param>
        public BotStateSet(params BotState[] botStates)
        {
            foreach (var botState in botStates)
            {
                BotStates.Add(botState.ContextServiceKey, botState);
            }
        }

        /// <summary>
        /// Gets or sets the BotStates list for the BotStateSet.
        /// </summary>
        /// <value>The BotState objects managed by this class.</value>
        public IDictionary<string, BotState> BotStates { get; set; } = new Dictionary<string, BotState>();

        /// <summary>
        /// Adds a bot state object to the set.
        /// </summary>
        /// <param name="botState">The bot state object to add.</param>
        /// <returns>The updated <see cref="BotStateSet"/>, so you can fluently call <see cref="Add(BotState)"/> multiple times.</returns>
        public BotStateSet Add(BotState botState)
        {
            ArgumentNullException.ThrowIfNull(botState);

            BotStates.Add(botState.ContextServiceKey, botState);
            return this;
        }

        /// <summary>
        /// Load all BotState records in parallel.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="force">should data be forced into cache.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task LoadAllAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            var tasks = BotStates.Select(bs => bs.Value.LoadAsync(turnContext, force, cancellationToken)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Save All BotState changes in parallel.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="force">should data be forced to save even if no change were detected.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task SaveAllChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            var tasks = BotStates.Select(bs => bs.Value.SaveChangesAsync(turnContext, force, cancellationToken)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}

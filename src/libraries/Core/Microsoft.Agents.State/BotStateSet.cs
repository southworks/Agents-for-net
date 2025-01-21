// Copyright (c) Microsoft. All rights reserved.
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
        private IDictionary<string, BotState> _scopes { get; set; } = new Dictionary<string, BotState>();

        /// <summary>
        /// Initializes a new instance of the <see cref="BotStateSet"/> class.
        /// </summary>
        /// <param name="botStates">initial list of <see cref="BotState"/> objects to manage.</param>
        public BotStateSet(params BotState[] botStates)
        {
            foreach (var botState in botStates)
            {
                _scopes.Add(botState.ContextServiceKey, botState);
            }
        }

        public T GetValue<T>(ITurnContext turnContext, string name, Func<T> defaultValueFactory)
        {
            var (scope, property) = GetScopeAndPath(name);
            return GetScope(scope).GetValue(property, defaultValueFactory);
        }

        public void SetValue(ITurnContext turnContext, string name, object value)
        {
            var (scope, property) = GetScopeAndPath(name);
            GetScope(scope).SetValue(property, value);
        }

        public BotState GetScope(string scope)
        {
            if (!_scopes.TryGetValue(scope, out BotState value))
            {
                throw new ArgumentException($"Scope '{scope}' not found");
            }
            return value;
        }

        private (string, string) GetScopeAndPath(string name)
        {
            var scopeEnd = name.IndexOf('.');
            if (scopeEnd == -1)
            {
                throw new ArgumentException("Path must include the state scope name");
            }
            return (name.Substring(0, scopeEnd), name.Substring(scopeEnd + 1));
        }

        public BotStateSet Add(BotState botState)
        {
            ArgumentNullException.ThrowIfNull(botState);
            _scopes.Add(botState.ContextServiceKey, botState);
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
            var tasks = _scopes.Select(bs => bs.Value.LoadAsync(turnContext, force, cancellationToken)).ToList();
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
            var tasks = _scopes.Select(kv => kv.Value.SaveChangesAsync(turnContext, force, cancellationToken)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}

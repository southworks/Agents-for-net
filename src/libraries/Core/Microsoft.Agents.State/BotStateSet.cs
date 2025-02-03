// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Agents.State
{
    /// <summary>
    ///  Manages a collection of botState and provides ability to load and save in parallel.
    /// </summary>
    public class BotStateSet : ITurnState
    {
        private readonly Dictionary<string, IBotState> _scopes = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="BotStateSet"/> class.
        /// </summary>
        /// <param name="botStates">initial list of <see cref="BotState"/> objects to manage.</param>
        public BotStateSet(params IBotState[] botStates)
        {
            foreach (var botState in botStates)
            {
                _scopes.Add(botState.Name, botState);
            }
        }

        /// <summary>
        /// Creates BotStateSet with default ConversationState and UserState
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="botStates">Additional list of BotState objects to manage.</param>
        public BotStateSet(IStorage storage, params IBotState[] botStates)
        {
            _scopes.Add(ConversationState.ScopeName, new ConversationState(storage));
            _scopes.Add(UserState.ScopeName, new UserState(storage));
            _scopes.Add(TempState.ScopeName, new TempState());

            foreach (var botState in botStates)
            {
                _scopes[botState.Name] = botState;
            }
        }

        public ConversationState Conversation => GetScope<ConversationState>();
        public UserState User => GetScope<UserState>();
        public PrivateConversationState Private => GetScope<PrivateConversationState>();
        public TempState Temp => GetScope<TempState>();

        public bool HasValue(string path)
        {
            var (scope, property) = GetScopeAndPath(path);
            return GetScope(scope).HasValue(property);
        }

        public T GetValue<T>(string name, Func<T> defaultValueFactory = null)
        {
            var (scope, property) = GetScopeAndPath(name);
            return GetScope(scope).GetValue(property, defaultValueFactory);
        }

        public void SetValue(string path, object value)
        {
            var (scope, property) = GetScopeAndPath(path);
            GetScope(scope).SetValue(property, value);
        }

        public void DeleteValue(string path)
        {
            var (scope, property) = GetScopeAndPath(path);
            GetScope(scope).DeleteValue(property);
        }

        public IBotState GetScope(string scope)
        {
            if (!_scopes.TryGetValue(scope, out IBotState value))
            {
                throw new ArgumentException($"Scope '{scope}' not found");
            }
            return value;
        }

        public T GetScope<T>()
        {
            foreach (var scope in _scopes)
            {
                if (scope.Value is T botState)
                {
                    return botState;
                }
            }

            throw new ArgumentException($"Scope '{nameof(T)}' not found");
        }

        public BotStateSet Add(IBotState botState)
        {
            ArgumentNullException.ThrowIfNull(botState);
            _scopes.Add(botState.Name, botState);
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
        public async Task LoadStateAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            var tasks = _scopes.Select(bs => bs.Value.LoadAsync(turnContext, force, cancellationToken)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public void ClearState(string scope)
        {
            GetScope(scope).ClearState();
        }

        /// <summary>
        /// Save All BotState changes in parallel.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="force">should data be forced to save even if no change were detected.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task SaveStateAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            var tasks = _scopes.Select(kv => kv.Value.SaveChangesAsync(turnContext, force, cancellationToken)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static (string, string) GetScopeAndPath(string name)
        {
            var scopeEnd = name.IndexOf('.');
            if (scopeEnd == -1)
            {
                throw new ArgumentException("Path must include the state scope name");
            }
            return (name.Substring(0, scopeEnd), name.Substring(scopeEnd + 1));
        }
    }
}

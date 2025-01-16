﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.State
{
    /// <summary>
    /// Defines a state management object and automates the reading and writing of associated state
    /// properties to a storage layer.
    /// </summary>
    /// <remarks>
    /// Each state management object defines a scope for a storage layer.
    ///
    /// State properties are created within a state management scope, and the Bot Framework
    /// defines these scopes:
    /// <see cref="ConversationState"/>, <see cref="UserState"/>, and <see cref="PrivateConversationState"/>.
    ///
    /// You can define additional scopes for your bot.
    /// </remarks>
    /// <seealso cref="IStorage"/>
    public abstract class BotState : IPropertyManager
    {
        private readonly string _contextServiceKey;
        private readonly IStorage _storage;

        /// <summary>
        /// Initializes a new instance of the <see cref="BotState"/> class.
        /// </summary>
        /// <param name="storage">The storage layer this state management object will use to store
        /// and retrieve state.</param>
        /// <param name="contextServiceKey">The key for the state cache for this <see cref="BotState"/>.</param>
        /// <remarks>This constructor creates a state management object and associated scope.
        /// The object uses <paramref name="storage"/> to persist state property values.
        /// The object uses the <paramref name="contextServiceKey"/> to cache state within the context for each turn.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="storage"/> or <paramref name="contextServiceKey"/>
        /// is <c>null</c>.</exception>
        /// <seealso cref="ITurnContext"/>
        public BotState(IStorage storage, string contextServiceKey)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _contextServiceKey = contextServiceKey ?? throw new ArgumentNullException(nameof(contextServiceKey));
        }

        public string ContextServiceKey {  get { return _contextServiceKey; } }

        /// <summary>
        /// Creates a named state property within the scope of a <see cref="BotState"/> and returns
        /// an accessor for the property.
        /// </summary>
        /// <typeparam name="T">The value type of the property.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <returns>An accessor for the property.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
        [Obsolete("Use BotState.GetPropertyAsync")]
        public IStatePropertyAccessor<T> CreateProperty<T>(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return new BotStatePropertyAccessor<T>(this, name);
        }

        /// <summary>
        /// Delete the property. The semantics are intended to be lazy, note the use of LoadAsync at the start.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="name">value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task DeletePropertyAsync(ITurnContext turnContext, string name, CancellationToken cancellationToken)
        {
            await LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
            await DeletePropertyValueAsync(turnContext, name, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the property value. The semantics are intended to be lazy, note the use of LoadAsync at the start.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="name">value.</param>
        /// <param name="defaultValueFactory">Defines the default value.
        /// Invoked when no value been set for the requested state property.
        /// If defaultValueFactory is defined as null in that case, the method returns null and</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<T> GetPropertyAsync<T>(ITurnContext turnContext, string name, Func<T> defaultValueFactory, CancellationToken cancellationToken)
        {
            T result = default;

            await LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);

            try
            {
                // if T is a value type, lookup up will throw key not found if not found, but as perf
                // optimization it will return null if not found for types which are not value types (string and object).
                result = await GetPropertyValueAsync<T>(turnContext, name, cancellationToken).ConfigureAwait(false);

                if (result == null && defaultValueFactory != null)
                {
                    // use default Value Factory and save default value for any further calls
                    result = defaultValueFactory();
                    await SetPropertyAsync(turnContext, name, result, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (KeyNotFoundException)
            {
                if (defaultValueFactory != null)
                {
                    // use default Value Factory and save default value for any further calls
                    result = defaultValueFactory();
                    await SetPropertyAsync(turnContext, name, result, cancellationToken).ConfigureAwait(false);
                }
            }

            return result;
        }

        /// <summary>
        /// Set the property value. The semantics are intended to be lazy, note the use of LoadAsync at the start.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="name">value.</param>
        /// <param name="value">value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task SetPropertyAsync<T>(ITurnContext turnContext, string name, T value, CancellationToken cancellationToken)
        {
            await LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
            await SetPropertyValueAsync(turnContext, name, value, cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Populates the state cache for this <see cref="BotState"/> from the storage layer.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="force">Optional, <c>true</c> to overwrite any existing state cache;
        /// or <c>false</c> to load state from storage only if the cache doesn't already exist.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        public virtual async Task LoadAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            var cachedState = GetCachedState(turnContext);
            var storageKey = GetStorageKey(turnContext);
            if (force || cachedState == null || cachedState.State == null)
            {
                var items = await _storage.ReadAsync([storageKey], cancellationToken).ConfigureAwait(false);
                items.TryGetValue(storageKey, out object val);

                if (val is IDictionary<string, object> asDictionary)
                {
                    turnContext.TurnState[_contextServiceKey] = new CachedBotState(asDictionary);
                }
                else if (val is JsonObject || val is JsonElement)
                {
                    // If types are not used by storage serialization, try deserializing to object
                    turnContext.TurnState[_contextServiceKey] = new CachedBotState(ProtocolJsonSerializer.ToObject<IDictionary<string, object>>(val));
                }
                else if (val == null)
                {
                    // This is the case where the dictionary did not exist in the store.
                    turnContext.TurnState[_contextServiceKey] = new CachedBotState();
                }
                else
                {
                    // This should never happen
                    throw new InvalidOperationException("Data is not in the correct format for BotState.");
                }
            }
        }

        /// <summary>
        /// Writes the state cache for this <see cref="BotState"/> to the storage layer.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="force">Optional, <c>true</c> to save the state cache to storage;
        /// or <c>false</c> to save state to storage only if a property in the cache has changed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        public virtual async Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            var cachedState = GetCachedState(turnContext);
            if (cachedState != null && (force || cachedState.IsChanged()))
            {
                var key = GetStorageKey(turnContext);
                var changes = new Dictionary<string, object>
                {
                    { key, cachedState.State },
                };
                await _storage.WriteAsync(changes, cancellationToken).ConfigureAwait(false);
                cachedState.Hash = CachedBotState.ComputeHash(cachedState.State);
                return;
            }
        }

        /// <summary>
        /// Clears the state cache for this <see cref="BotState"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>This method clears the state cache in the turn context. Call
        /// <see cref="SaveChangesAsync(ITurnContext, bool, CancellationToken)"/> to persist this
        /// change in the storage layer.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        public virtual Task ClearStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            // Explicitly setting the hash will mean IsChanged is always true. And that will force a Save.
            turnContext.TurnState[_contextServiceKey] = new CachedBotState { Hash = string.Empty };

            return Task.CompletedTask;
        }

        /// <summary>
        /// Deletes any state in storage and the cache for this <see cref="BotState"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        public virtual async Task DeleteStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            var cachedState = GetCachedState(turnContext);
            if (cachedState != null)
            {
                turnContext.TurnState.Remove(_contextServiceKey);
            }

            var storageKey = GetStorageKey(turnContext);
            await _storage.DeleteAsync(new[] { storageKey }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a copy of the raw cached data for this <see cref="BotState"/> from the turn context.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <returns>A JSON representation of the cached state.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        internal JsonElement Get(ITurnContext turnContext)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            var cachedState = GetCachedState(turnContext);
            return JsonSerializer.SerializeToElement(cachedState.State, ProtocolJsonSerializer.SerializationOptions);
        }

        /// <summary>
        /// Gets the cached bot state instance that wraps the raw cached data for this <see cref="BotState"/>
        /// from the turn context.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <returns>The cached bot state instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        internal CachedBotState GetCachedState(ITurnContext turnContext)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            return turnContext.TurnState.Get<CachedBotState>(_contextServiceKey);
        }

        /// <summary>
        /// When overridden in a derived class, gets the key to use when reading and writing state to and from storage.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <returns>The storage key.</returns>
        protected abstract string GetStorageKey(ITurnContext turnContext);

        /// <summary>
        /// Gets the value of a property from the state cache for this <see cref="BotState"/>.
        /// </summary>
        /// <typeparam name="T">The value type of the property.</typeparam>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful, the result contains the property value, otherwise it will be default(T).</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> or
        /// <paramref name="propertyName"/> is <c>null</c>.</exception>
#pragma warning disable CA1801 // Review unused parameters (we can't change this without breaking binary compat)
        protected Task<T> GetPropertyValueAsync<T>(ITurnContext turnContext, string propertyName, CancellationToken cancellationToken = default)
#pragma warning restore CA1801 // Review unused parameters
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

            var cachedState = GetCachedState(turnContext);

            if (cachedState.State.TryGetValue(propertyName, out object result))
            {
                if (result is T t)
                {
                    return Task.FromResult(t);
                }

                if (result == null)
                {
                    return Task.FromResult(default(T));
                }

                // If types are not used by storage serialization try to convert the object to the type expected
                // using the serializer.
                var converted = ProtocolJsonSerializer.ToObject<T>(result);
                cachedState.State[propertyName] = converted;
                return Task.FromResult(converted);
            }

            if (typeof(T).IsValueType)
            {
                throw new KeyNotFoundException(propertyName);
            }

            return Task.FromResult(default(T));
        }

        /// <summary>
        /// Deletes a property from the state cache for this <see cref="BotState"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> or
        /// <paramref name="propertyName"/> is <c>null</c>.</exception>
        protected Task DeletePropertyValueAsync(ITurnContext turnContext, string propertyName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

            var cachedState = GetCachedState(turnContext);
            cachedState.State.Remove(propertyName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the value of a property in the state cache for this <see cref="BotState"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="value">The value to set on the property.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="turnContext"/> or
        /// <paramref name="propertyName"/> is <c>null</c>.</exception>
        protected Task SetPropertyValueAsync(ITurnContext turnContext, string propertyName, object value, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

            var cachedState = GetCachedState(turnContext);
            cachedState.State[propertyName] = value;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Internal cached bot state.
        /// </summary>
        internal class CachedBotState
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CachedBotState"/> class.
            /// </summary>
            /// <param name="state">Initial state for the <see cref="CachedBotState"/>.</param>
            public CachedBotState(IDictionary<string, object> state = null)
            {
                State = state ?? new Dictionary<string, object>();
                Hash = ComputeHash(State);
            }

            /// <summary>
            /// Gets or sets the state as a dictionary of key value pairs.
            /// </summary>
            /// <value>
            /// The state as a dictionary of key value pairs.
            /// </value>
#pragma warning disable CA2227 // Collection properties should be read only (we can't change this without breaking binary compat)
            public IDictionary<string, object> State { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

            internal string Hash { get; set; }

            internal static string ComputeHash(object obj)
            {
                return ProtocolJsonSerializer.ToJson(obj);
            }

            internal bool IsChanged()
            {
                return Hash != ComputeHash(State);
            }
        }

        #region Obsolete BotStatePropertyAccessor
        /// <summary>
        /// Implements an <see cref="IStatePropertyAccessor{T}"/> for a property container.
        /// Note the semantics of this accessor are intended to be lazy, this means the Get, Set and Delete
        /// methods will first call LoadAsync. This will be a no-op if the data is already loaded.
        /// The implication is you can just use this accessor in the application code directly without first calling LoadAsync
        /// this approach works with the AutoSaveStateMiddleware which will save as needed at the end of a turn.
        /// </summary>
        /// <typeparam name="T">type of value the propertyAccessor accesses.</typeparam>
        private class BotStatePropertyAccessor<T> : IStatePropertyAccessor<T>
        {
            private BotState _botState;

            public BotStatePropertyAccessor(BotState botState, string name)
            {
                _botState = botState;
                Name = name;
            }

            /// <summary>
            /// Gets name of the property.
            /// </summary>
            /// <value>
            /// name of the property.
            /// </value>
            public string Name { get; private set; }

            /// <summary>
            /// Delete the property. The semantics are intended to be lazy, note the use of LoadAsync at the start.
            /// </summary>
            /// <param name="turnContext">The turn context.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
            public async Task DeleteAsync(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                await _botState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
                await _botState.DeletePropertyValueAsync(turnContext, Name, cancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// Get the property value. The semantics are intended to be lazy, note the use of LoadAsync at the start.
            /// </summary>
            /// <param name="turnContext">The context object for this turn.</param>
            /// <param name="defaultValueFactory">Defines the default value.
            /// Invoked when no value been set for the requested state property.
            /// If defaultValueFactory is defined as null in that case, the method returns null and
            /// <see cref="SetAsync(ITurnContext, T, CancellationToken)">SetAsync</see> is not called.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
            public async Task<T> GetAsync(ITurnContext turnContext, Func<T> defaultValueFactory, CancellationToken cancellationToken)
            {
                T result = default(T);
                
                await _botState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);

                try
                {
                    // if T is a value type, lookup up will throw key not found if not found, but as perf
                    // optimization it will return null if not found for types which are not value types (string and object).
                    result = await _botState.GetPropertyValueAsync<T>(turnContext, Name, cancellationToken).ConfigureAwait(false);

                    if (result == null && defaultValueFactory != null)
                    {
                        // use default Value Factory and save default value for any further calls
                        result = defaultValueFactory();
                        await SetAsync(turnContext, result, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (KeyNotFoundException)
                {
                    if (defaultValueFactory != null)
                    {
                        // use default Value Factory and save default value for any further calls
                        result = defaultValueFactory();
                        await SetAsync(turnContext, result, cancellationToken).ConfigureAwait(false);
                    }
                }

                return result;
            }

            /// <summary>
            /// Set the property value. The semantics are intended to be lazy, note the use of LoadAsync at the start.
            /// </summary>
            /// <param name="turnContext">turn context.</param>
            /// <param name="value">value.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
            public async Task SetAsync(ITurnContext turnContext, T value, CancellationToken cancellationToken)
            {
                await _botState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
                await _botState.SetPropertyValueAsync(turnContext, Name, value, cancellationToken).ConfigureAwait(false);
            }
        }
        #endregion
    }
}

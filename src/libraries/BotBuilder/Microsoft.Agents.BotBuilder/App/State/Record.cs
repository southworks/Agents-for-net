// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.BotBuilder.App.State
{
    /// <summary>
    /// The class representing a record.
    /// </summary>
    public class Record : Dictionary<string, object>
    {
        /// <summary>
        /// Tries to get the value from the dictionary.
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="key">key to look for</param>
        /// <param name="value">value associated with key</param>
        /// <returns>True if a value of given type is associated with key.</returns>
        /// <exception cref="InvalidCastException"></exception>
        public bool TryGetValue<T>(string key, out T value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (base.TryGetValue(key, out object entry))
            {
                if (entry is T castedEntry)
                {
                    value = castedEntry;
                    return true;
                };

                //throw new InvalidCastException($"Failed to cast generic object to type '{typeof(T)}'");
                value = ProtocolJsonSerializer.ToObject<T>(entry);
                return true;
            }

#pragma warning disable CS8601 // Possible null reference assignment.
            value = default;
#pragma warning restore CS8601 // Possible null reference assignment.

            return false;
        }

        /// <summary>
        /// Gets the value from the dictionary.
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="key">key to look for</param>
        /// <returns>The value associated with the key</returns>
        public T? Get<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (TryGetValue(key, out T value))
            {
                return value;
            }
            else
            {
                return default;
            };
        }

        /// <summary>
        /// Sets value in the dictionary.
        /// </summary>
        /// <typeparam name="T">Type of value</typeparam>
        /// <param name="key">key to look for</param>
        /// <param name="value">value associated with key</param>
        public void Set<T>(string key, T value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);

#pragma warning disable CS8601 // Possible null reference assignment.
            this[key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.
        }
    }
}

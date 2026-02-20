// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Represents the type of orchestration operation.
    /// Implemented as a struct wrapping a string for forward compatibility with new operation types.
    /// </summary>
    [JsonConverter(typeof(OrchestrationOperationJsonConverter))]
    public readonly struct OrchestrationOperation : IEquatable<OrchestrationOperation>
    {
        /// <summary>
        /// Creates a new conversation / starts a session.
        /// </summary>
        public static readonly OrchestrationOperation StartConversation = new("StartConversation");

        /// <summary>
        /// Invokes a tool (topic) by schema name.
        /// </summary>
        public static readonly OrchestrationOperation InvokeTool = new("InvokeTool");

        /// <summary>
        /// Forwards a user message to an in-progress conversation.
        /// </summary>
        public static readonly OrchestrationOperation HandleUserResponse = new("HandleUserResponse");

        /// <summary>
        /// Sends a conversation update event to the bot.
        /// </summary>
        public static readonly OrchestrationOperation ConversationUpdate = new("ConversationUpdate");

        private readonly string? _value;

        /// <summary>
        /// Creates a new <see cref="OrchestrationOperation"/> with the specified value.
        /// </summary>
        /// <param name="value">The string value of the operation.</param>
        public OrchestrationOperation(string? value)
        {
            _value = value;
        }

        /// <summary>
        /// Gets the string value of the operation.
        /// </summary>
        public string Value => _value ?? string.Empty;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is OrchestrationOperation other && Equals(other);

        /// <inheritdoc/>
        public bool Equals(OrchestrationOperation other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
#if !NETSTANDARD
            return Value.GetHashCode(StringComparison.OrdinalIgnoreCase);
#else
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
#endif
        }

        /// <inheritdoc/>
        public override string ToString() => Value;

        /// <summary>Equality operator.</summary>
        public static bool operator ==(OrchestrationOperation left, OrchestrationOperation right) => left.Equals(right);

        /// <summary>Inequality operator.</summary>
        public static bool operator !=(OrchestrationOperation left, OrchestrationOperation right) => !left.Equals(right);
    }

    /// <summary>
    /// JSON converter for <see cref="OrchestrationOperation"/>.
    /// </summary>
    public class OrchestrationOperationJsonConverter : JsonConverter<OrchestrationOperation>
    {
        /// <inheritdoc/>
        public override OrchestrationOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new OrchestrationOperation(reader.GetString());
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, OrchestrationOperation value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }
}

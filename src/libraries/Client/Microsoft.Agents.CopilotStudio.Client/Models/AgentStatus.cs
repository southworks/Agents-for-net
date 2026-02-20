// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Represents the status of an externally orchestrated agent.
    /// Implemented as a struct wrapping a string for forward compatibility.
    /// </summary>
    [JsonConverter(typeof(AgentStatusJsonConverter))]
    public readonly struct AgentStatus : IEquatable<AgentStatus>
    {
        /// <summary>
        /// Topic finished execution; the orchestrator resumes with updated agent context.
        /// </summary>
        public static readonly AgentStatus Completed = new("Completed");

        /// <summary>
        /// Topic requires additional user input. Next user message goes directly to Copilot Studio Runtime.
        /// </summary>
        public static readonly AgentStatus WaitingForUserInput = new("WaitingForUserInput");

        /// <summary>
        /// Topic halted waiting for a long-running operation. User input triggers "interruption".
        /// </summary>
        public static readonly AgentStatus InProgress = new("InProgress");

        /// <summary>
        /// Unknown or unrecognized status.
        /// </summary>
        public static readonly AgentStatus Unknown = new(null);

        private readonly string? _value;

        /// <summary>
        /// Creates a new <see cref="AgentStatus"/> with the specified value.
        /// </summary>
        /// <param name="value">The string value of the status.</param>
        public AgentStatus(string? value)
        {
            _value = value;
        }

        /// <summary>
        /// Gets the string value of the status.
        /// </summary>
        public string Value => _value ?? string.Empty;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is AgentStatus other && Equals(other);

        /// <inheritdoc/>
        public bool Equals(AgentStatus other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

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
        public static bool operator ==(AgentStatus left, AgentStatus right) => left.Equals(right);

        /// <summary>Inequality operator.</summary>
        public static bool operator !=(AgentStatus left, AgentStatus right) => !left.Equals(right);
    }

    /// <summary>
    /// JSON converter for <see cref="AgentStatus"/>.
    /// </summary>
    public class AgentStatusJsonConverter : JsonConverter<AgentStatus>
    {
        /// <inheritdoc/>
        public override AgentStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new AgentStatus(reader.GetString());
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, AgentStatus value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }
}

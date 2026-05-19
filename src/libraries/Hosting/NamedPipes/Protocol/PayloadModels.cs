// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.NamedPipes.Protocol
{
    /// <summary>
    /// Describes a data stream attached to a request or response payload.
    /// </summary>
    public sealed class PayloadDescription
    {
        /// <summary>
        /// Gets or sets the stream identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content type of the stream.
        /// </summary>
        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the length of the stream data in bytes.
        /// </summary>
        [JsonPropertyName("length")]
        public int? Length { get; set; }
    }

    /// <summary>
    /// JSON payload for a Request frame (Type='A').
    /// </summary>
    public sealed class RequestPayload
    {
        /// <summary>
        /// Gets or sets the HTTP verb (e.g., POST, GET).
        /// </summary>
        [JsonPropertyName("verb")]
        public string Verb { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request path.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of associated data streams.
        /// </summary>
        [JsonPropertyName("streams")]
        public List<PayloadDescription> Streams { get; set; }
    }

    /// <summary>
    /// JSON payload for a Response frame (Type='B').
    /// </summary>
    public sealed class ResponsePayload
    {
        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the list of associated data streams.
        /// </summary>
        [JsonPropertyName("streams")]
        public List<PayloadDescription> Streams { get; set; }
    }
}

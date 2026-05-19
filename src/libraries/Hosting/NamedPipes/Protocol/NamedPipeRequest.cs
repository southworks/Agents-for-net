// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Hosting.NamedPipes.Protocol
{
    /// <summary>
    /// Represents an incoming request received from the named pipe transport.
    /// </summary>
    public sealed class NamedPipeRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for this request.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the HTTP verb (e.g., POST).
        /// </summary>
        public string Verb { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request path (e.g., /api/messages).
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request body bytes.
        /// </summary>
        public byte[] Body { get; set; }
    }
}

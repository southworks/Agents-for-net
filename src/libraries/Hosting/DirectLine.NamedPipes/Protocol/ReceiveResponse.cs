// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol
{
    /// <summary>
    /// Represents an incoming response received from the named pipe transport
    /// (for outbound requests sent by the agent).
    /// </summary>
    internal sealed class ReceiveResponse
    {
        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the response body bytes.
        /// </summary>
        public byte[] Body { get; set; }
    }
}

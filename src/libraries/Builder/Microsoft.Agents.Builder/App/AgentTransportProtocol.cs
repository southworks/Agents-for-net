// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;

namespace Microsoft.Agents.Builder.App
{

    /// <summary>
    /// A transport protocol for an Agent.
    /// </summary>
    public enum AgentTransportProtocol
    {
        /// <summary>
        /// JSON-RPC over HTTP transport protocol.
        /// </summary>
        JSONRPC,
        /// <summary>
        /// gRPC transport protocol.
        /// </summary>
        GRPC,
        /// <summary>
        /// HTTP with JSON payloads transport protocol.
        /// </summary>
        HttpJson,
        /// <summary>
        /// Activity Protocol transport protocol.
        /// </summary>
        ActivityProtocol
    }
}
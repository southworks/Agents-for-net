// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// Header name constants used in HTTP requests and responses for Copilot Studio communication.
    /// </summary>
    public static class CopilotStudioHeaderNames
    {
        /// <summary>
        /// Header for the conversation ID.
        /// Maps the BotFramework conversation ID to outgoing requests.
        /// </summary>
        public const string ConversationId = "x-ms-conversation-id";

        /// <summary>
        /// Header for client request correlation ID.
        /// Used for request tracing and diagnostics.
        /// </summary>
        public const string ClientRequestId = "x-ms-client-request-id";
    }
}

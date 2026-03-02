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
        /// Header that maps to the BotFramework conversation ID.
        /// Used to pass the conversation ID in outgoing requests.
        /// </summary>
        public const string BotFrameworkConversationIdRequestHeader = "x-ms-conversation-id";

        /// <summary>
        /// Header for client request correlation ID.
        /// Used for request tracing and diagnostics.
        /// </summary>
        public const string XMsClientRequestId = "x-ms-client-request-id";
    }
}

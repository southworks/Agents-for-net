// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Contains the Agent Channel information for active Conversations.
    /// </summary>
    public class ChannelConversation
    {
        public string ChannelName { get; internal set; }

        /// <summary>
        /// This is the conversationId created with <see cref="IAgentHost.GetOrCreateConversationAsync(BotBuilder.ITurnContext, BotBuilder.State.ConversationState, string, System.Threading.CancellationToken)"/>.
        /// </summary>
        public string ChannelConversationId { get; internal set; }
    }
}

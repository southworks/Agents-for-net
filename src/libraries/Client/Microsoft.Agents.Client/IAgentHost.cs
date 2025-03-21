// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Represents a host the contains IAgentChannels for multi-Agent.
    /// </summary>
    public interface IAgentHost
    {
        /// <summary>
        /// The endpoint to use in Activity.ServiceUrl if unspecified in a Channels settings.
        /// </summary>
        Uri DefaultHostEndpoint { get; set; }

        string HostClientId { get; set; }

        /// <summary>
        /// Gets the Agent Client used to send Activities to another Agent.
        /// </summary>
        /// <param name="name">The name of the Agent</param>
        IAgentChannel GetChannel(string name);

        /// <summary>
        /// Returns a list of configured Agents.
        /// </summary>
        IList<IAgentChannelInfo> GetChannels();

        /// <summary>
        /// Returns the conversationId for an existing conversation for a Channel, relative to to the current Turns Conversation.
        /// </summary>
        /// <remarks>
        /// IChannelHost currently only supports a single active conversation per Channel per Turn Conversation.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="conversationState"></param>
        /// <param name="channelName"></param>
        /// <returns>conversationId for an existing conversation, or null.</returns>
        string GetExistingConversation(ITurnContext turnContext, ConversationState conversationState, string channelName);

        /// <summary>
        /// Returns a list of all Channel conversations for the current Turns Conversation.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="conversationState">Typically from <see cref="ITurnState.Conversation"/></param>
        /// <param name="channelName">A Channel name from configuration.</param>
        /// <returns>Non-null list of Channel conversations.</returns>
        IList<ChannelConversation> GetExistingConversations(ITurnContext turnContext, ConversationState conversationState);

        /// <summary>
        /// Returns the existing conversation for a Channel, or creates a new one.
        /// </summary>
        /// <remarks>
        /// IChannelHost currently only supports a single active conversation per Channel per Turn Conversation.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="conversationState">Typically from <see cref="ITurnState.Conversation"/></param>
        /// <param name="channelName">A Channel name from configuration.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> GetOrCreateConversationAsync(ITurnContext turnContext, ConversationState conversationState, string channelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the indicated conversation.
        /// </summary>
        /// <remarks>
        /// Only the bot knows when a conversation is done.  All effort should be made to remove conversations as otherwise the persisted conversations accumulate.
        /// A received Activity of type EndOfConversation is one instance where the conversation should be deleted.
        /// </remarks>
        /// <param name="channelConversationId">A conversationId return from <see cref="GetExistingConversation"/> or <see cref="GetOrCreateConversationAsync"/>.</param>
        /// <param name="conversationState">Typically from <see cref="ITurnState.Conversation"/></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DeleteConversationAsync(string channelConversationId, ConversationState conversationState, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends all active conversations for the Turn conversation.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="conversationState"></param>
        /// <param name="cancellationToken"></param>
        Task EndAllActiveConversations(ITurnContext turnContext, ConversationState conversationState, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the conversation information for the specified channel conversation.
        /// </summary>
        /// <param name="channelConversationId"></param>
        /// <param name="cancellationToken"></param>
        Task<ChannelConversationReference> GetChannelConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an activity to an Agent.
        /// </summary>
        /// <remarks>
        /// This is used for Activity.DeliverMode == 'normal'.  In order to get the asynchronous replies from the Agent, the
        /// <see cref="AgentResponses.OnAgentReply"/> handler must be set.
        /// </remarks>
        /// <remarks>
        /// This will not properly handle Invoke or ExpectReplies requests as it's doesn't return a value.  Use <see cref="GetChannel(string)"/> and 
        /// use the returned <see cref="IAgentChannel"/> directly for those.
        /// </remarks>
        /// <param name="channelName">A Channel name from configuration.</param>
        /// <param name="channelConversationId"><see cref="GetOrCreateConversationAsync"/> or <see cref="GetExistingConversation"/></param>
        /// <param name="activity"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="ArgumentException">If the specified channelName is null or not found.</exception>
        Task SendToAgent(string channelName, string channelConversationId, IActivity activity, CancellationToken cancellationToken = default);
    }
}

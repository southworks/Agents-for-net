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
    /// Represents a host the contains IChannels for Bot-to-bot.
    /// </summary>
    public interface IChannelHost
    {
        /// <summary>
        /// The endpoint to use in Activity.ServiceUrl if unspecified in a Channels settings.
        /// </summary>
        Uri DefaultHostEndpoint { get; set; }

        string HostClientId { get; set; }

        IChannel GetChannel(string name);

        /// <summary>
        /// Returns the conversationId for an existing conversation for a Channel, relative to to the current Turns Conversation.
        /// </summary>
        /// <remarks>
        /// IChannelHost currently only supports a single active conversation per Channel (for the current Host conversation).
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
        /// <param name="conversationState"></param>
        /// <param name="channelName"></param>
        /// <returns>Non-null list of Channel conversations.</returns>
        IList<ChannelConversation> GetExistingConversations(ITurnContext turnContext, ConversationState conversationState);

        Task<string> GetOrCreateConversationAsync(ITurnContext turnContext, ConversationState conversationState, string channelName, CancellationToken cancellationToken = default);
        Task DeleteConversationAsync(string channelConversationId, ConversationState conversationState, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the conversation information for the specified channel conversation.
        /// </summary>
        /// <param name="channelConversationId"></param>
        /// <param name="cancellationToken"></param>
        Task<BotConversationReference> GetBotConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an activity to a Channel.
        /// </summary>
        /// <remarks>
        /// This is used for Activity.DeliverMode == 'normal'.  In order to get the asynchronous replies from the Channel, the
        /// <see cref="BotResponses.OnBotReply"/> handler must be set.
        /// </remarks>
        /// <remarks>
        /// This will not properly handle Invoke or ExpectReplies requests as it's doesn't return a value.  Use <see cref="GetChannel(string)"/> and 
        /// use the returned <see cref="IChannel"/> directly.
        /// </remarks>
        /// <param name="channelName"></param>
        /// <param name="channelConversationId"><see cref="GetOrCreateConversationAsync"/> or <see cref="GetExistingConversation"/></param>
        /// <param name="activity"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="ArgumentException">If the specified channelName is null or not found.</exception>
        Task SendToChannel(string channelName, string channelConversationId, IActivity activity, CancellationToken cancellationToken = default);
    }
}

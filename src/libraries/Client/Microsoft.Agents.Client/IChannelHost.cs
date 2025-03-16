// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Security.Claims;
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
        Uri DefaultHostEndpoint { get; }

        string HostClientId { get; }

        IChannel GetChannel(string name);

        /// <summary>
        /// Returns the conversationId for an existing conversation with channel.
        /// </summary>
        /// <remarks>
        /// IChannelHost currently only supports a single active conversation per Channel (for the current Host conversation).
        /// </remarks>
        /// <param name="channelName"></param>
        /// <param name="state"></param>
        /// <returns>conversationId for an existing conversation, or null.</returns>
        string GetExistingConversation(string channelName, ITurnState state);

        Task<string> GetOrCreateConversationAsync(string channelName, ITurnState state, ClaimsIdentity identity, IActivity activity, CancellationToken cancellationToken = default);
        Task DeleteConversationAsync(string channelConversationId, ITurnState state, CancellationToken cancellationToken = default);

        Task<BotConversationReference> GetBotConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken = default);

        Task SendToChannel(string channelName, string channelConversationId, IActivity activity, CancellationToken cancellationToken = default);
    }
}

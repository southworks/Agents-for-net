// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Defines the interface of a factory that is used to create unique conversation IDs for Channel conversations.
    /// </summary>
    internal interface IConversationIdFactory
    {
        /// <summary>
        /// Creates a conversation id for a bot conversation.
        /// </summary>
        /// <param name="options">A <see cref="ConversationIdFactoryOptions"/> instance containing parameters for creating the conversation ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A unique conversation ID used to communicate with an Agent.</returns>
        /// <remarks>
        /// It should be possible to use the returned string on a request URL and it should not contain special characters. 
        /// </remarks>
        Task<string> CreateConversationIdAsync(ConversationIdFactoryOptions options, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the <see cref="ChannelConversationReference"/> used during <see cref="CreateConversationIdAsync(ConversationIdFactoryOptions,System.Threading.CancellationToken)"/> for a Channel ConversationId.
        /// </summary>
        /// <param name="channelConversationId">A conversationId created using <see cref="CreateConversationIdAsync(ConversationIdFactoryOptions,System.Threading.CancellationToken)"/>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The caller's <see cref="ConversationReference"/> for a conversationId, with originatingAudience. Null if not found.</returns>
        Task<ChannelConversationReference> GetChannelConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a <see cref="ConversationReference"/>.
        /// </summary>
        /// <param name="channelConversationId">A channel conversationId created using <see cref="CreateConversationIdAsync(ConversationIdFactoryOptions,System.Threading.CancellationToken)"/>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task DeleteConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken);
    }
}

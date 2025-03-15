// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Client
{
    public interface IChannel : IDisposable
    {
        string Alias { get; }

        string DisplayName { get; }

        /// <summary>
        /// Sends an Activity with DeliveryMode "normal" or "expectReplies".  For `normal`, this would require handling of async replies via IChannelApiHandler via ChannelApiController.
        /// </summary>
        /// <remarks>This is a rather base level of functionality and in most cases <see cref="SendActivityForResultAsync"/> is easier to use.</remarks>
        /// <param name="channelConversationId"></param>
        /// <param name="activity"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="relatesTo"></param>
        /// <returns></returns>
        Task<InvokeResponse<T>> SendActivityAsync<T>(string channelConversationId, IActivity activity, CancellationToken cancellationToken, IActivity relatesTo = null);

        /// <summary>
        /// Sends an Activity with DeliveryMode "normal" or "expectReplies". Convenience method when a result is not expected.
        /// </summary>
        /// <param name="channelConversationId"></param>
        /// <param name="activity"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="relatesTo"></param>
        /// <returns></returns>
        Task SendActivityAsync(string channelConversationId, IActivity activity, CancellationToken cancellationToken, IActivity relatesTo = null);
    }
}

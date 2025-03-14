﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder
{
    public interface IStreamingResponse
    {
        /// <summary>
        /// Set IActivity that will be (optionally) used for the final streaming message.
        /// </summary>
        IActivity FinalMessage { get; set; }

        /// <summary>
        /// The interval in milliseconds at which intermediate messages are sent.
        /// </summary>
        /// <remarks>
        /// Teams default: 1000
        /// WebChat default: 500
        /// </remarks>
        int Interval { get; set; }

        /// <summary>
        /// Indicate if the current channel supports intermediate messages.
        /// </summary>
        /// <remarks>
        /// Channels that don't support intermediate messages will buffer
        /// text, and send a normal final message when EndStreamAsync is called.
        /// </remarks>
        bool IsStreamingChannel { get; }

        /// <summary>
        /// The buffered message.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Ends the stream by sending the final message to the client.
        /// </summary>
        /// <remarks>
        /// Since the messages are sent on an interval, this call will block until all have been sent
        /// before sending the final Message.
        /// </remarks>
        /// <returns>A Task representing the async operation</returns>
        /// <exception cref="InvalidOperationException">Throws if the stream has already ended.</exception>
        Task EndStreamAsync(int timeout = -1, CancellationToken cancellationToken = default);

        bool IsStreamStarted();

        /// <summary>
        /// Queues an informative update to be sent to the client.
        /// </summary>
        /// <param name="text">Text of the update to send.</param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="InvalidOperationException">Throws if the stream has already ended.</exception>
        Task QueueInformativeUpdateAsync(string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queues a chunk of partial message text to be sent to the client.
        /// </summary>
        /// <param name="text">Partial text of the message to send.</param>
        /// <param name="citations">Citations to include in the message.</param>
        /// <exception cref="InvalidOperationException">Throws if the stream has already ended.</exception>
        void QueueTextChunk(string text);

        /// <summary>
        /// Reset an already used stream.  If the stream is still running, this will wait for completion.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ResetAsync(int timeout = -1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the number of updates sent for the stream.
        /// </summary>
        /// <returns>Number of updates sent so far.</returns>
        int UpdatesSent();
    }
}
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Defines per-channel timing parameters for the typing worker.
    /// </summary>
    public interface ITypingChannelStrategy
    {
        /// <summary>
        /// Delay in milliseconds before the first typing activity is sent.
        /// </summary>
        int InitialDelayMs { get; }

        /// <summary>
        /// Interval in milliseconds between subsequent typing activities.
        /// </summary>
        int IntervalMs { get; }
    }
}

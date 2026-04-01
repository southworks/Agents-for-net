// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Concrete per-channel typing strategy with configurable initial delay and interval.
    /// </summary>
    public sealed class TypingChannelStrategy(int initialDelayMs, int intervalMs) : ITypingChannelStrategy
    {
        /// <inheritdoc/>
        public int InitialDelayMs => initialDelayMs;

        /// <inheritdoc/>
        public int IntervalMs => intervalMs;
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// A single declarative channel-adapter registration: the <c>channelId</c> the adapter handles and
    /// the CLR adapter type that handles it. Built from <see cref="ChannelAdapterAttribute"/> (via the
    /// source-generated <see cref="ChannelAdapterInitAssemblyAttribute"/>) or supplied explicitly through
    /// dependency injection.
    /// </summary>
    /// <param name="ChannelId">The <c>channelId</c> this adapter handles (e.g., "msteams", "a2a").</param>
    /// <param name="AdapterType">The adapter CLR type. Must implement <see cref="Microsoft.Agents.Builder.IChannelAdapter"/>.</param>
    public sealed record ChannelAdapterRegistration(string ChannelId, Type AdapterType);
}

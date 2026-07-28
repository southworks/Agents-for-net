// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Slack.Api;

namespace Microsoft.Agents.Extensions.Slack;

/// <summary>
/// Provides Slack-specific helpers for working with the current <see cref="ITurnContext"/>.
/// </summary>
public interface ISlackTurnContext : ITurnContext
{
    /// <summary>
    /// Gets the <see cref="SlackApi"/> client registered for Slack API access in the current turn context.
    /// </summary>
    SlackApi Client { get; }

    /// <summary>
    /// Gets the strongly-typed Slack channel data (envelope / interactive payload) carried on the current Activity.
    /// </summary>
    SlackChannelData SlackChannelData { get; }
}

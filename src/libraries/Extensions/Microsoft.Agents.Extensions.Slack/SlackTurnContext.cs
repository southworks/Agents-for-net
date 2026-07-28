// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;

namespace Microsoft.Agents.Extensions.Slack;

/// <summary>
/// A Slack-aware <see cref="ITurnContext"/> that exposes Slack-specific helpers such as the Slack API client
/// and strongly-typed channel data on top of the underlying turn context.
/// </summary>
public class SlackTurnContext : TurnContextWrapper, ISlackTurnContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SlackTurnContext"/> class wrapping the specified inner turn context.
    /// </summary>
    /// <param name="turnContext">The inner turn context to wrap.</param>
    public SlackTurnContext(ITurnContext turnContext) : base(turnContext)
    {
    }

    /// <inheritdoc/>
    public SlackApi Client => _turnContext.Services.Get<SlackApi>();

    /// <inheritdoc/>
    public SlackChannelData SlackChannelData => _turnContext.Activity.GetChannelData<SlackChannelData>();
}

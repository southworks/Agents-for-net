// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;

namespace Microsoft.Agents.Extensions.Slack;

internal static class HandlerUtils
{
    public static RouteHandler WrapHandler(SlackRouteHandler handler)
    {
        return async (ctx, turnState, cancellationToken) =>
        {
            var stc = new SlackTurnContext(ctx);
            await handler(stc, turnState, cancellationToken);
        };
    }

    public static FeedbackLoopHandler WrapHandler(SlackFeedbackLoopHandler handler)
    {
        return async (ctx, turnState, feedbackData, cancellationToken) =>
        {
            var stc = new SlackTurnContext(ctx);
            await handler(stc, turnState, feedbackData, cancellationToken);
        };
    }
}

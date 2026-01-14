// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A
{
    public static class AgentApplicationsExtensions
    {
        public static RouteSelector ForA2A(this string activityType)
        {
            return async (context, ct) =>
            {
                return context.Activity.ChannelId == Channels.A2A && context.Activity.Type == activityType;
            };
        }
    }
}

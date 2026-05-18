// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    internal class ScopeContinueConversation : TelemetryScope
    {

        private readonly string _conversationId;
        private readonly IActivity _activity;

        public ScopeContinueConversation(string conversationId, IActivity activity) : base(Constants.ScopeContinueConversation)
        {
            _conversationId = conversationId;
            _activity = activity;
        }

        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.ConversationId, _conversationId);
            activity.SetTag(TagNames.ActivityType, _activity.Type);
            activity.SetTag(TagNames.ActivityChannelId, _activity.ChannelId.ToString());
        }
    }
}
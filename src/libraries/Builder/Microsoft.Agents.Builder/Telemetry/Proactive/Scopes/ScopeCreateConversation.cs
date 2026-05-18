// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    internal class ScopeCreateConversation : TelemetryScope
    {

        private readonly CreateConversationOptions _options;

        public ScopeCreateConversation(CreateConversationOptions options) : base(Constants.ScopeCreateConversation)
        {
            _options = options;
        }

        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.ActivityChannelId, _options.ChannelId);
            activity.SetTag(TagNames.MembersCount, _options.Parameters.Members.Count);
        }
    }
}
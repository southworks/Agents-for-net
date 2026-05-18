// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    internal class ScopeDeleteConversation : TelemetryScope
    {

        private readonly string _conversationId;

        public ScopeDeleteConversation(string conversationId) : base(Constants.ScopeDeleteConversation)
        {
            _conversationId = conversationId;
        }

        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.ConversationId, _conversationId);
        }
    }
}
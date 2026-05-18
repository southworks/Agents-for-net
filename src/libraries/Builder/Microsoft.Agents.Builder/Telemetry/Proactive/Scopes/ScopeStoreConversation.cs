// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    internal class ScopeStoreConversation : TelemetryScope
    {

        private readonly string _conversationId;

        public ScopeStoreConversation(string conversationId) : base(Constants.ScopeStoreConversation)
        {
            _conversationId = conversationId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            base.Callback(activity, duration, exception);
        }
    }
}
// Copyright (c) Microsoft Corporation. All rights reserved.
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces storage of a conversation reference for
    /// later proactive use.
    /// </summary>
    /// <remarks>
    /// This scope currently relies on the base <see cref="TelemetryScope"/> behavior and
    /// does not add additional tags.
    /// </remarks>
    internal class ScopeStoreConversation : TelemetryScope
    {

        private readonly string _conversationId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeStoreConversation"/> class.
        /// </summary>
        /// <param name="conversationId">The identifier of the conversation being stored.</param>
        public ScopeStoreConversation(string conversationId) : base(Constants.ScopeStoreConversation)
        {
            _conversationId = conversationId;
        }

        /// <inheritdoc />
        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            base.Callback(activity, duration, exception);
        }
    }
}
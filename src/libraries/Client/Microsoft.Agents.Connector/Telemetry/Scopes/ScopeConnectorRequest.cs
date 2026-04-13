using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeConnectorRequest : TelemetryScope
    {
        private readonly string? _conversationId;
        private readonly string? _activityId;

        public ScopeConnectorRequest(string scopeName, string? conversationId = null, string? activityId = null) : base(scopeName)
        {
            _conversationId = conversationId;
            _activityId = activityId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            TagList metricTags = new();

            if (_conversationId != null)
            {
                metricTags.Add(TagNames.ConversationId, _conversationId ?? TelemetryUtils.Unknown);
            }

            if (_activityId != null)
            {
                metricTags.Add(TagNames.ActivityId, _activityId ?? TelemetryUtils.Unknown);
            }

            Metrics.ConnectorRequestCount.Add(1, metricTags);
            Metrics.ConnectorRequestDuration.Record(duration, metricTags);
        }
    }
}

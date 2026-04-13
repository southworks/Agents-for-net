using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeUserTokenRestClientRequest : TelemetryScope
    {
        private readonly string? _connectionName;
        private readonly string? _userId;
        private readonly string? _channelId;

        public ScopeUserTokenRestClientRequest(string spanName, string? connectionName = null, string? userId = null, string? channelId = null)
            : base(spanName)
        {
            _connectionName = connectionName;
            _userId = userId;
            _channelId = channelId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            if (_connectionName != null)
            {
                activity.SetTag(TagNames.AuthHandlerId, _connectionName);
            }
            if (_userId != null)
            {
                activity.SetTag(TagNames.UserId, _userId);
            }
            if (_channelId != null)
            {
                activity.SetTag(TagNames.ActivityChannelId, _channelId);
            }
            Metrics.UserTokenRestClientRequestCount.Add(1);
            Metrics.UserTokenRestClientRequestDuration.Record(duration);
        }
    }
}

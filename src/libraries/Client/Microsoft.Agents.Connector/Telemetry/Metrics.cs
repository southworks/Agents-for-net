using System.Diagnostics.Metrics;
using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Connector.Telemetry
{
    internal static class Metrics
    {
        internal static Counter<long> ConnectorRequestCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricConnectorRequestCount,
            unit: "request",
            description: "Total number of requests made by connector clients"
            );

        internal static Histogram<double> ConnectorRequestDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricConnectorRequestDuration,
            unit: "ms",
            description: "Duration of requests made by connector clients."
            );

        internal static Counter<long> UserTokenRestClientRequestCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricUserTokenRestClientRequestCount,
            unit: "request",
            description: "Total number of requests made by user token rest client"
            );

        internal static Histogram<double> UserTokenRestClientRequestDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricUserTokenRestClientRequestDuration,
            unit: "ms",
            description: "Duration of requests made by user token rest client."
            );
    }
}

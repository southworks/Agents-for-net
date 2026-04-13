#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeDeleteActivity : ScopeConnectorRequest
    {
        public ScopeDeleteActivity(string conversationId, string activityId)
            : base(Constants.ScopeDeleteActivity, conversationId, activityId)
        { }
    }
}

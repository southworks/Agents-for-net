#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeUpdateActivity : ScopeConnectorRequest
    {
        public ScopeUpdateActivity(string conversationId, string activityId)
            : base(Constants.ScopeUpdateActivity, conversationId, activityId)
        { }
    }
}

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeReplyToActivity : ScopeConnectorRequest
    {
        public ScopeReplyToActivity(string conversationId, string activityId)
            : base(Constants.ScopeReplyToActivity, conversationId, activityId)
        {}
    }
}

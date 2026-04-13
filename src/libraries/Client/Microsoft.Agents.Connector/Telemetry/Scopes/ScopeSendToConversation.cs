#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeSendToConversation : ScopeConnectorRequest
    {
        public ScopeSendToConversation(string conversationId, string? activityId)
            : base(Constants.ScopeSendToConversation, conversationId, activityId)
        { }
    }
}

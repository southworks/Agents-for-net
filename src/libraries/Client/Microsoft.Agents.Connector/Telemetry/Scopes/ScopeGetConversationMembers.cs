namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeGetConversationMembers : ScopeConnectorRequest
    {
        public ScopeGetConversationMembers(string conversationId) : base(Constants.ScopeGetConversationMembers, conversationId)
        { }
    }
}

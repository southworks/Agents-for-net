namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeUploadAttachment : ScopeConnectorRequest
    {
        public ScopeUploadAttachment(string conversationId) : base(Constants.ScopeUploadAttachment, conversationId)
        { }
    }
}

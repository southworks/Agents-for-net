#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeGetAadTokens : ScopeUserTokenRestClientRequest
    {
        public ScopeGetAadTokens(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeGetAadTokens, connectionName, userId, channelId)
        { }
    }
}

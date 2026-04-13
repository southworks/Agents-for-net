#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeGetToken : ScopeUserTokenRestClientRequest
    {
        public ScopeGetToken(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeGetToken, connectionName, userId, channelId)
        { }
    }
}

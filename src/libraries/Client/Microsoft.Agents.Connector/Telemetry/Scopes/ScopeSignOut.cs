#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeSignOut : ScopeUserTokenRestClientRequest
    {
        public ScopeSignOut(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeSignOut, connectionName, userId, channelId)
        { }
    }
}

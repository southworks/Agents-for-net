#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeGetSignInResource : ScopeUserTokenRestClientRequest
    {
        public ScopeGetSignInResource(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeGetSignInResource, connectionName, userId, channelId)
        { }
    }
}

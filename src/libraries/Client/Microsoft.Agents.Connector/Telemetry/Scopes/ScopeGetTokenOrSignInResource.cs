#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeGetTokenOrSignInResource : ScopeUserTokenRestClientRequest
    {
        public ScopeGetTokenOrSignInResource(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeGetTokenOrSignInResource, connectionName, userId, channelId)
        { }
    }
}

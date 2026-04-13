#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeGetTokenStatus : ScopeUserTokenRestClientRequest
    {
        public ScopeGetTokenStatus(string userId, string? channelId = null)
            : base(Constants.ScopeGetTokenStatus, userId, channelId)
        { }
    }
}

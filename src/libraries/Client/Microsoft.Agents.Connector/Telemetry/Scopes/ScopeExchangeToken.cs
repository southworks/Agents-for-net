#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeExchangeToken : ScopeUserTokenRestClientRequest
    {
        public ScopeExchangeToken(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeExchangeToken, connectionName, userId, channelId)
        { }
    }
}

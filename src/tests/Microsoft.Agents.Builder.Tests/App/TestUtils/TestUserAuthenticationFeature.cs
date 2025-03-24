
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;

namespace Microsoft.Agents.Builder.Tests.App.TestUtils
{
    internal sealed class TestUserAuthenticationFeature : UserAuthorizationFeature
    {
        public TestUserAuthenticationFeature(AgentApplication app, UserAuthorizationOptions options) : base(app, options)
        {
        }
    }
}

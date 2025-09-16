// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A365
{
    /// <summary>
    /// AgentExtension for Microsoft A365.
    /// </summary>
    public class A365Extension : IAgentExtension
    {
        public static readonly RouteSelector AgenticMessage = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Message && AgenticAuthorization.IsAgenticRequest(tc));
        public static readonly RouteSelector AgenticWord = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Message && AgenticAuthorization.IsAgenticRequest(tc) && tc.Activity.ChannelId == Channels.AgentsWord);
        public static readonly RouteSelector AgenticExcel = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Message && AgenticAuthorization.IsAgenticRequest(tc) && tc.Activity.ChannelId == Channels.AgentsWord);
        public static readonly RouteSelector AgenticEmail = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Message && AgenticAuthorization.IsAgenticRequest(tc) && tc.Activity.ChannelId == Channels.AgentsEmail);
        public static readonly RouteSelector AgenticPowerPoint = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Message && AgenticAuthorization.IsAgenticRequest(tc) && tc.Activity.ChannelId == Channels.AgentsPowerPoint);

        private readonly AgentApplication _app;

        public A365Extension(AgentApplication app)
        {
            AssertionHelpers.ThrowIfNull(app, nameof(app));
            _app = app;
        }


        #region IAgentExtension
#if !NETSTANDARD
        public ChannelId ChannelId { get; init; } = "*";
#else
        public ChannelId ChannelId { get; set; } = "*";
#endif


        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = 32767, string[] autoSignInHandlers = null)
        {
            var ensureAgentic = new RouteSelector(async (turnContext, cancellationToken) => {
                return AgenticAuthorization.IsAgenticRequest(turnContext) && await routeSelector(turnContext, cancellationToken);
            });

            agentApplication.AddRoute(ensureAgentic, routeHandler, isInvokeRoute, rank, autoSignInHandlers);
        }
        #endregion

        public void OnActivity(string activityType, RouteHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null, ChannelId channelId = null)
        {
            AddRoute(_app, (tc, ct) => Task.FromResult(tc.Activity.IsType(activityType) && (channelId == null || tc.Activity.ChannelId == channelId)), routeHandler, false, rank, autoSignInHandlers);
        }

        public void OnAgenticWord(RouteHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null)
        {
            AddRoute(_app, AgenticWord, routeHandler, false, rank, autoSignInHandlers);
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A365
{
    /// <summary>
    /// AgentExtension for Microsoft A365.
    /// </summary>
    public class A365Extension : IAgentExtension
    {
        private readonly IConnections _connections;

        /// <summary>
        /// Creates a new A365Extension instance.
        /// To leverage this extension, call <see cref="AgentApplication.RegisterExtension(IAgentExtension)"/> with an instance of this class.
        /// </summary>
        /// <param name="agentApplication">The agent application to leverage for route registration.</param>
        /// <param name="connections"></param>
        public A365Extension(IConnections connections)
        {
            _connections = connections;
        }

        public static bool IsAgenticRequest(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            return IsAgenticRequest(turnContext.Activity);
        }

        public static bool IsAgenticRequest(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            return activity?.Recipient?.Role == RoleTypes.AgentIdentity
                || activity?.Recipient?.Role == RoleTypes.AgentUser;
        }

        public static string GetAgentInstanceId(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.AadClientId;
        }

        public static string GetAgentUser(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.Name;  // What the demo is using for AU UserUpn
        }

        public async Task<string> GetAgentInstanceTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext))
            {
                return null;
            }

            var connection = _connections.GetTokenProvider(turnContext.Identity, "agentic");
            if (connection is IAgenticTokenProvider agenticTokenProvider)
            {
                return await agenticTokenProvider.GetAgenticInstanceTokenAsync(GetAgentInstanceId(turnContext), cancellationToken);
            }

            throw new InvalidOperationException("Connection doesn't support IAgenticTokenProvider");
        }

        public async Task<string> GetAgentUserTokenAsync(ITurnContext turnContext, IList<string> scopes, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext) || string.IsNullOrEmpty(GetAgentUser(turnContext)))
            {
                return null;
            }

            var connection = _connections.GetTokenProvider(turnContext.Identity, "agentic");
            if (connection is IAgenticTokenProvider agenticTokenProvider)
            {
                return await agenticTokenProvider.GetAgenticUserTokenAsync(GetAgentInstanceId(turnContext), GetAgentUser(turnContext), scopes, cancellationToken);
            }

            throw new InvalidOperationException("Connection doesn't support IAgenticTokenProvider");
        }

        #region IAgentExtension
#if !NETSTANDARD
        public AgentApplication AgentApplication { get; set; }
        public ChannelId ChannelId { get; init; } = "*";
#else
        protected AgentApplication AgentApplication { get; set;}
        public ChannelId ChannelId { get; set; } = "*";
#endif


        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = 32767, string[] autoSignInHandlers = null)
        {
            var ensureAgentic = new RouteSelector(async (turnContext, cancellationToken) => {
                return IsAgenticRequest(turnContext) && await routeSelector(turnContext, cancellationToken);
            });

            agentApplication.AddRoute(ensureAgentic, routeHandler, isInvokeRoute, rank, autoSignInHandlers);
        }
        #endregion

        public void OnActivity(string activityType, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = 32767, string[] autoSignInHandlers = null, ChannelId channelId = null)
        {
            AddRoute(AgentApplication, (tc, ct) => Task.FromResult(tc.Activity.IsType(activityType) && channelId == null || tc.Activity.ChannelId == channelId), routeHandler, isInvokeRoute, rank, autoSignInHandlers);
        }
    }
}

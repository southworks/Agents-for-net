// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Ignore Spelling: Agentic

using AgentNotification.Extensions;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Kairo.Sdk.AgentsSdkExtensions.Models;

namespace AgentNotification
{
    /// <summary>
    /// AgentsSdkExtension for Kairo.
    /// </summary>
    public class Agents365 : AgentExtension
    {
        private static readonly string ExtentionChannelId = "agents";
        private readonly AgentApplication _app;

        // Supported subchannels
        private const string AgentsEmailSubChannel = "email";
        private const string AgentsExcelSubChannel = "excel";
        private const string AgentsWordSubChannel = "word";
        private const string AgentsPowerPointSubChannel = "powerpoint";

        public Agents365(AgentApplication app)
        {
            AssertionHelpers.ThrowIfNull(app, nameof(app));
            _app = app;
            ChannelId = new ChannelId(ExtentionChannelId);
            ChannelId.SubChannel = "*";
        }

        #region IAgentExtension

//#if !NETSTANDARD
//        public ChannelId ChannelId { get; init; } = new ChannelId(ExtentionChannelId);
//#else
//        public ChannelId ChannelId { get; set; } = new ChannelId(ExtentionChannelId);
//#endif

        /*
        public static readonly RouteSelector AgenticMessage = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Message && tc.Activity.IsAgenticRequest());
        public static readonly RouteSelector AgenticEvent = (tc, ct) => Task.FromResult(tc.Activity.Type == ActivityTypes.Event && tc.Activity.IsAgenticRequest());
        public static readonly RouteSelector AgenticWord = (tc, ct) => Task.FromResult(tc.Activity.IsAgenticRequest() && tc.Activity.ChannelId?.SubChannel == Channels.AgentsWord);
        public static readonly RouteSelector AgenticExcel = (tc, ct) => Task.FromResult(tc.Activity.IsAgenticRequest() && tc.Activity.ChannelId == Channels.AgentsWord);
        public static readonly RouteSelector AgenticEmail = (tc, ct) => Task.FromResult(tc.Activity.IsAgenticRequest() && tc.Activity.ChannelId == Channels.AgentsEmail);
        public static readonly RouteSelector AgenticPowerPoint = (tc, ct) => Task.FromResult(tc.Activity.IsAgenticRequest() && tc.Activity.ChannelId == Channels.AgentsPowerPoint);
        */

        //public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = 32767, string[] autoSignInHandlers = null)
        //{
        //    var ensureAgentic = new RouteSelector(async (turnContext, cancellationToken) => {
        //        return AgenticAuthorization.IsAgenticRequest(turnContext)
        //        && ContainsValidEntity(turnContext.Activity)
        //        && await routeSelector(turnContext, cancellationToken);
        //    });

        //    agentApplication.AddRoute(ensureAgentic, routeHandler, isInvokeRoute, rank, autoSignInHandlers);
        //}

        #endregion

        /// <summary>
        /// Register a route handler for agent notifications from a specific sub-channel or all known subchannels for a given agent channel.
        /// </summary>
        /// <param name="subChannelId"></param>
        /// <param name="routeHandler"></param>
        /// <param name="rank"></param>
        /// <param name="autoSignInHandlers"></param>
        public Agents365 OnAgentNotification(string subChannelId, AgentNotificationHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null!)
        {
            RouteSelector routeSelector = (tc, ct) => 
                Task.FromResult(
                    IsChannelForMe(tc.Activity) && 
                    (subChannelId.Equals("*") || IsForKnownSubChannel(tc.Activity, subChannelId))
                );

            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                // Wrap the activity in an AgentNotificationActivity
                var agentNotificationActivity = new AgentNotificationActivity(turnContext.Activity);
                // for now, we will required the handler to return the proper result.. we will change this later to return a structured result and handle the response back. 
                await handler(turnContext, turnState, agentNotificationActivity, cancellationToken);
            };
            AddRoute(_app, routeSelector, routeHandler, false, rank, autoSignInHandlers);
            return this;
        }

        //private bool ContainsValidEntity(IActivity activity)
        //{
        //    if (activity.Entities == null || activity.Entities.Count == 0)
        //    {
        //        return false;
        //    }
        //    string entityType = GetEntityType(activity.ChannelId);
        //    return activity.Entities.FirstOrDefault(e => string.Equals(e.Type, entityType, StringComparison.OrdinalIgnoreCase)) != null;
        //}

        //private string GetEntityType(string channelId)
        //{
        //    return channelId switch
        //    {
        //        Channels.AgentsWord => "wxpcomment",
        //        Channels.AgentsExcel => "wxpcomment",
        //        Channels.AgentsPowerPoint => "wxpcomment",
        //        Channels.AgentsEmail => "emailnotification",
        //        _ => string.Empty,
        //    };
        //}

        private bool IsChannelForMe(IActivity agentActivity)
        {
            return agentActivity.ChannelId != null 
                   && agentActivity.ChannelId.Channel != null
                   && agentActivity.ChannelId.Channel.Equals(ExtentionChannelId, StringComparison.OrdinalIgnoreCase);

        }

        private bool IsForKnownSubChannel(IActivity agentActivity, string subChannelId)
        {
            if (string.IsNullOrEmpty(subChannelId))
            {
                return false;
            }
            if (!IsValidSubChannel(subChannelId))
            {
                return false;
            }
            return agentActivity.ChannelId != null
                    && agentActivity.ChannelId.Channel != null
                    && agentActivity.ChannelId.SubChannel != null
                    && agentActivity.ChannelId.SubChannel.Equals(subChannelId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidSubChannel(string subChannel)
        {
            return subChannel switch
            {
                AgentsEmailSubChannel => true,
                AgentsExcelSubChannel => true,
                AgentsWordSubChannel => true,
                AgentsPowerPointSubChannel => true,
                _ => false,
            };
        }
    }

    public static class AgentNotificationExtensions
    {
        public static void OnAgentNotification(this AgentApplication app, ChannelId channelId, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new Agents365(app), a365 =>
            {
                a365.OnAgentNotification(channelId, routeHandler, rank, autoSignInHandlers);
            });
    }
}

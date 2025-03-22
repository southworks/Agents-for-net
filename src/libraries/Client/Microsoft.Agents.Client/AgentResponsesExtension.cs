// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Microsoft.Agents.Client
{
    public delegate Task AgentResponseHandler(ITurnContext turnContext, ITurnState turnState, AgentConversationReference reference, IActivity agentActivity, CancellationToken cancellationToken);

    public class AgentResponsesExtension(AgentApplication agentApplication) : IAgentExtension
    {
        /// <summary>
        /// Provides a handler for when an Agent sends an Activity when Activity.DeliverMode == `normal` (asynchronous HTTP POST back to the channel host.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="handler"></param>
        /// <param name="rank"></param>
        public void OnAgentReply(AgentResponseHandler handler, ushort rank = RouteRank.First)
        {
            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                var channelResponse = ProtocolJsonSerializer.ToObject<AdapterChannelResponseHandler.ChannelReply>(turnContext.Activity.Value);
                await handler(turnContext, turnState, channelResponse.ChannelConversationReference, channelResponse.Activity, cancellationToken).ConfigureAwait(false);
            }

            agentApplication.OnActivity(
                (turnContext, CancellationToken) =>
                    Task.FromResult(string.Equals(ActivityTypes.Event, turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(AdapterChannelResponseHandler.ChannelReplyEventName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase)),
                routeHandler,
                rank);
        }

        public string ChannelId { get; init; } = "*";

        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false)
        {
            agentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute);
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;

namespace Microsoft.Agents.Client
{
    public delegate Task AgentResponseHandler(ITurnContext turnContext, ITurnState turnState, ChannelConversationReference reference, IActivity botActivity, CancellationToken cancellationToken);

    /// <summary>
    /// Handles routing response from another Agent to AgentApplication.
    /// </summary>
    /// <code>
    /// class MyAgent : AgentApplication
    /// {
    ///     public MyAgent(AgentApplicationOptions options) : base(options)
    ///     {
    ///         ChannelResponses.OnChannelReply(OnChannelResponseAsync);
    ///     }
    ///     
    ///     private async Task OnChannelResponseAsync(ITurnContext turnContext, ITurnState turnState, ChannelConversationReference reference, IActivity channelActivity, CancellationToken cancellationToken)
    ///     {
    ///         // do something with the response
    ///     }
    /// }
    /// </code>
    public static class ChannelResponses
    {
        /// <summary>
        /// Provides a handler for when an Agent sends an Activity when Activity.DeliverMode == `normal` (asynchronous HTTP POST back to the channel host.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="handler"></param>
        /// <param name="rank"></param>
        public static void OnChannelReply(this AgentApplication app, AgentResponseHandler handler, ushort rank = RouteRank.First)
        {
            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                var channelResponse = ProtocolJsonSerializer.ToObject<AdapterChannelResponseHandler.ChannelReply>(turnContext.Activity.Value);
                await handler(turnContext, turnState, channelResponse.ChannelConversationReference, channelResponse.Activity, cancellationToken).ConfigureAwait(false);
            }

            app.OnActivity(
                (turnContext, CancellationToken) =>
                    Task.FromResult(string.Equals(ActivityTypes.Event, turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(AdapterChannelResponseHandler.ChannelReplyEventName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase)),
                routeHandler,
                rank);
        }
    }
}

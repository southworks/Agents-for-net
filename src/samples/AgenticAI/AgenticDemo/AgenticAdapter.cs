// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

namespace AgenticAI.AgenticDemo
{
    /// <summary>
    /// Temporary Adapter that can repsond to Teams using the Agentic AI tokens and Graph.
    /// </summary>
    public class AgenticAdapter : CloudAdapter
    {
        private readonly AgenticAuthorization _agenticAuthorization;

        public AgenticAdapter(
            IChannelServiceClientFactory channelServiceClientFactory, 
            IActivityTaskQueue activityTaskQueue, 
            ILogger<CloudAdapter> logger = null, 
            AdapterOptions options = null, 
            IMiddleware[] middlewares = null, 
            IConfiguration config = null,
            IConnections connections = null)
            : base(channelServiceClientFactory, activityTaskQueue, logger, options, middlewares, config)
        {
            _agenticAuthorization = new AgenticAuthorization(connections);
        }

        protected override async Task<bool> HostResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            if (!AgenticAuthorization.IsAgenticRequest(incomingActivity) || incomingActivity.DeliveryMode == DeliveryModes.ExpectReplies)
            {
                return await base.HostResponseAsync(incomingActivity, outActivity, cancellationToken);
            }

            return true;
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            if (!AgenticAuthorization.IsAgenticRequest(turnContext) || turnContext.Activity.DeliveryMode == DeliveryModes.ExpectReplies)
            {
                return await base.SendActivitiesAsync(turnContext, activities, cancellationToken);
            }

            foreach (var activity in activities)
            {
                // Create the chat message.  This is only sending Activity.Text.
                var chatMessage = new ChatMessage
                {
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Text,
                        Content = activity.Text
                    }
                };

                var graphClient = _agenticAuthorization.GraphClientForAgentUser(turnContext, ["https://canary.graph.microsoft.com/.default"]);

                // Send the message to the chat
                await graphClient.Chats[turnContext.Activity.Conversation.Id].Messages
                    .PostAsync(chatMessage);
            }

            return [];
        }
    }
}

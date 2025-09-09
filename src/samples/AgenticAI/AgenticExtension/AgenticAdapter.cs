// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A365;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

namespace AgenticAI.AgenticExtension
{
    /// <summary>
    /// Temporary Adapter that can repsond to Teams using the Agentic AI tokens and Graph.
    /// </summary>
    public class AgenticAdapter : CloudAdapter
    {
        private readonly A365Extension _a365;

        public AgenticAdapter(
            IChannelServiceClientFactory channelServiceClientFactory, 
            IActivityTaskQueue activityTaskQueue, 
            ILogger<CloudAdapter> logger = null, 
            AdapterOptions options = null, 
            IMiddleware[] middlewares = null, 
            IConfiguration config = null,
            A365Extension a365 = null)
            : base(channelServiceClientFactory, activityTaskQueue, logger, options, middlewares, config)
        {
            _a365 = a365;
        }

        protected override async Task<bool> HostResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            if (!A365Extension.IsAgenticRequest(incomingActivity))
            {
                return await base.HostResponseAsync(incomingActivity, outActivity, cancellationToken);
            }

            return true;
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            if (!A365Extension.IsAgenticRequest(turnContext))
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

                var graphClient = _a365.GraphClientForAgentUser(turnContext, ["https://canary.graph.microsoft.com/.default"]);

                // Send the message to the chat
                await graphClient.Chats[turnContext.Activity.Conversation.Id].Messages
                    .PostAsync(chatMessage);
            }

            return [];
        }
    }
}

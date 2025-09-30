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
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

#nullable disable

namespace AgentNotification
{
    /// <summary>
    /// Temporary Adapter that can repsond to Teams using the Agentic AI tokens and Graph.
    /// </summary>
    public class AgenticAdapter(
        IChannelServiceClientFactory channelServiceClientFactory,
        IActivityTaskQueue activityTaskQueue,
        ILogger<CloudAdapter> logger = null,
        AdapterOptions options = null,
        IMiddleware[] middlewares = null,
        IConfiguration config = null,
        IConnections connections = null) : CloudAdapter(channelServiceClientFactory, activityTaskQueue, logger, options, middlewares, config)
    {
        private readonly AgenticAuthorization _agenticAuthorization = new(connections);

        protected override async Task<bool> HostResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            if (!ShouldRespondHere(incomingActivity))
            {
                return await base.HostResponseAsync(incomingActivity, outActivity, cancellationToken);
            }

            return true;
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            if (!ShouldRespondHere(turnContext.Activity))
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

                var graphClient = GraphClientForAgentUser(turnContext, ["https://canary.graph.microsoft.com/.default"]);

                // Send the message to the chat
                await graphClient.Chats[turnContext.Activity.Conversation.Id].Messages
                    .PostAsync(chatMessage, cancellationToken: cancellationToken);
            }

            return [];
        }

        private static bool ShouldRespondHere(IActivity activity)
        {
            return AgenticAuthorization.IsAgenticRequest(activity) && activity.ChannelId == Channels.Msteams;
        }

        private GraphServiceClient GraphClientForAgentUser(ITurnContext turnContext, IList<string> scopes)
        {
            try
            {
                // Create an async token provider that calls GetAgenticUserTokenAsync each time
                var authProvider = new ManualTokenAuthenticationProvider(() =>
                {
                    return _agenticAuthorization.GetAgenticUserTokenAsync(turnContext, scopes);
                });
                return new GraphServiceClient(authProvider, baseUrl: "https://canary.graph.microsoft.com/testprodbetateamsgraphdev");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating GraphServiceClient with agentic user identity: {ex.Message}");
                throw;
            }
        }
    }

    class ManualTokenAuthenticationProvider(Func<Task<string>> AccessTokenProvider) : IAuthenticationProvider
    {
        public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            var accessToken = await AccessTokenProvider();
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
        }
    }
}

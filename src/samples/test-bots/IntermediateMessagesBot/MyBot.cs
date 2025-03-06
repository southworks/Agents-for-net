// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.AI;

namespace IntermediateMessagesBot
{
    public class MyBot : AgentApplication
    {
        private readonly IChatClient _chatClient;

        public MyBot(AgentApplicationOptions options, IChatClient chatClient) : base(options)
        {
            _chatClient = chatClient;

            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
            OnActivity(ActivityTypes.Message, OnMessageAsync);
        }

        protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Say anything and I'll recite poetry."), cancellationToken);
                }
            }
        }

        protected async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            StreamingResponse response = new StreamingResponse(turnContext);

            await response.QueueInformativeUpdate("Hold on for an awesome poem...", cancellationToken);

            await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteStreamingAsync(
                "Write a poem about why Microsoft Agents SDK is so great.",
                new ChatOptions { MaxOutputTokens = 1000 },
                cancellationToken: cancellationToken))
            {
                var text = update.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    response.QueueTextChunk(text);
                }
            }

            await response.EndStreamAsync(cancellationToken);
        }
    }
}

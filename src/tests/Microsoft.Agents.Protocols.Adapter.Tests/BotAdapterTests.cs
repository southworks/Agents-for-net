// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Adapters;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.Bot.Builder.Tests;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Protocols.Adapter.Tests
{
    public class BotAdapterTests
    {
        [Fact]
        public void Use_ShouldAddSingleMiddleware()
        {
            var adapter = new SimpleAdapter();
            var middleware = new CallCountingMiddleware();
            adapter.Use(middleware);
            Assert.Single((MiddlewareSet)adapter.MiddlewareSet);
        }

        [Fact]
        public void Use_ShouldAddChainingMiddlewares()
        {
            var adapter = new SimpleAdapter();
            adapter.Use(new CallCountingMiddleware()).Use(new CallCountingMiddleware());
            Assert.Equal(2, ((MiddlewareSet)adapter.MiddlewareSet).Count());
        }

        [Fact]
        public async Task SendActivityAsync_ShouldPassResourceResponsesThrough()
        {
            void ValidateResponses(IActivity[] activities)
            {
                // no need to do anything.
            }

            var adapter = new SimpleAdapter(ValidateResponses);
            var context = new TurnContext(adapter, new Activity());

            var activityId = Guid.NewGuid().ToString();
            var activity = TestMessage.Message();
            activity.Id = activityId;

            var resourceResponse = await context.SendActivityAsync(activity);
            Assert.True(resourceResponse.Id == activityId, "Incorrect response Id returned");
        }

        [Fact]
        public async Task ProcessRequest_ShouldGetLocaleFromActivity()
        {
            void ValidateResponses(IActivity[] activities)
            {
                // no need to do anything.
            }

            var adapter = new SimpleAdapter(ValidateResponses);
            var context = new TurnContext(adapter, new Activity());

            var activityId = Guid.NewGuid().ToString();
            var activity = TestMessage.Message();
            activity.Id = activityId;
            activity.Locale = "de-DE";

            Task SimpleCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                Assert.Equal("de-DE", turnContext.Activity.Locale);
                return Task.CompletedTask;
            }

            await adapter.ProcessRequest(activity, SimpleCallback, default(CancellationToken));
        }

        [Fact]
        public async Task ContinueConversationAsync_ShouldExecuteCallback()
        {
            bool callbackInvoked = false;
            var adapter = new TestAdapter(TestAdapter.CreateConversation("ContinueConversation_DirectMsgAsync"));
            ConversationReference convReference = new ConversationReference
            {
                ActivityId = "activityId",
                Bot = new ChannelAccount
                {
                    Id = "channelId",
                    Name = "testChannelAccount",
                    Role = "bot",
                },
                ChannelId = "testChannel",
                ServiceUrl = "testUrl",
                Conversation = new ConversationAccount
                {
                    ConversationType = string.Empty,
                    Id = "testConversationId",
                    IsGroup = false,
                    Name = "testConversationName",
                    Role = "user",
                },
                User = new ChannelAccount
                {
                    Id = "channelId",
                    Name = "testChannelAccount",
                    Role = "bot",
                },
            };
            Task ContinueCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }

            await adapter.ContinueConversationAsync("MyBot", convReference, ContinueCallback, default(CancellationToken));
            Assert.True(callbackInvoked);
        }
    }
}

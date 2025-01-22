// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class CloudAdapterTests
    {
        [Fact]
        public void Constructor_ShouldThrowWithNullActivityTaskQueue()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            Assert.Throws<ArgumentNullException>(() => new CloudAdapter(factory.Object, null));
        }

        [Fact]
        public void OnTurnError_ShouldSetMiddlewares()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            var logger = new Mock<ILogger<IBotHttpAdapter>>();
            var middleware = new Mock<Core.Interfaces.IMiddleware>();
            var adapter = new CloudAdapter(factory.Object, queue.Object, logger.Object, true, middleware.Object);

            Assert.Single(adapter.MiddlewareSet as IEnumerable<Core.Interfaces.IMiddleware>);
        }

        [Fact]
        public async Task OnTurnError_ShouldSendExceptionActivity()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            var logger = new Mock<ILogger<IBotHttpAdapter>>();
            var adapter = new CloudAdapter(factory.Object, queue.Object, logger.Object);

            var context = new Mock<ITurnContext>();
            context.Setup(e => e.SendActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.Once);
            context.Setup(e => e.TraceActivityAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.Once);
            var exception = new ErrorResponseException("test") { Body = new ErrorResponse() };

            await adapter.OnTurnError(context.Object, exception);

            Mock.Verify(context);
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullHttpRequest()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            var adapter = new CloudAdapter(factory.Object, queue.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.ProcessAsync(null, null, null, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullHttpResponse()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            var adapter = new CloudAdapter(factory.Object, queue.Object);
            var request = new Mock<HttpRequest>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.ProcessAsync(request.Object, null, null, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullBot()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            var adapter = new CloudAdapter(factory.Object, queue.Object);
            var request = new Mock<HttpRequest>();
            var response = new Mock<HttpResponse>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.ProcessAsync(request.Object, response.Object, null, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetMethodNotAllowedStatus()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();

            var adapter = new CloudAdapter(factory.Object, queue.Object);
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetBadRequestStatus()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();

            var adapter = new CloudAdapter(factory.Object, queue.Object);
            var bot = new ActivityHandler();
            var activity = new Activity();
            var activitySerialized = JsonConvert.SerializeObject(activity);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(activitySerialized));
            context.Request.Method = HttpMethods.Post;

            await adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetUnauthorized()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            queue.Setup(e => e.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>()))
                .Throws(new UnauthorizedAccessException())
                .Verifiable(Times.Once);

            var adapter = new TestAdapter(factory.Object, queue.Object);
            var bot = new ActivityHandler();
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Conversation = new ConversationAccount { Id = "test" },
            };
            var activitySerialized = JsonConvert.SerializeObject(activity);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(activitySerialized));
            context.Request.Method = HttpMethods.Post;

            await adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            Mock.Verify(queue);
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetInvokeResponse()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();

            var adapter = new TestAdapter(factory.Object, queue.Object);
            var bot = new ActivityHandler();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Conversation = new ConversationAccount { Id = "test" },
            };
            var activitySerialized = JsonConvert.SerializeObject(activity);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(activitySerialized));
            context.Request.Method = HttpMethods.Post;

            await adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(999, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_ShouldQueueActivity()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            queue.Setup(e => e.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>()))
                .Verifiable(Times.Once);

            var adapter = new TestAdapter(factory.Object, queue.Object);
            var bot = new ActivityHandler();
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Conversation = new ConversationAccount { Id = "test" },
            };
            var activitySerialized = JsonConvert.SerializeObject(activity);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(activitySerialized));
            context.Request.Method = HttpMethods.Post;

            await adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
            Mock.Verify(queue);
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingActivity()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            var logger = new Mock<ILogger<IBotHttpAdapter>>();
            logger.Setup(e => e.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().StartsWith("BadRequest: Missing Conversation.Id")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);

            var adapter = new CloudAdapter(factory.Object, queue.Object, logger.Object);
            var bot = new ActivityHandler();
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
            };
            var activitySerialized = JsonConvert.SerializeObject(activity);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(activitySerialized));
            context.Request.Method = HttpMethods.Post;

            await adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            Mock.Verify(queue, logger);
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingConversationId()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            var logger = new Mock<ILogger<IBotHttpAdapter>>();
            logger.Setup(e => e.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().StartsWith("BadRequest: Missing activity")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);

            var adapter = new CloudAdapter(factory.Object, queue.Object, logger.Object);
            var bot = new ActivityHandler();
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(""));
            context.Request.Method = HttpMethods.Post;

            await adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            Mock.Verify(queue, logger);
        }

        private class TestAdapter : CloudAdapter
        {
            public TestAdapter(IChannelServiceClientFactory factory, IActivityTaskQueue queue) : base(factory, queue)
            {
            }

            public override Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, BotCallbackHandler callback, CancellationToken cancellationToken)
            {
                return Task.FromResult(new InvokeResponse { Status = 999, Body = activity });
            }
        }
    }
}
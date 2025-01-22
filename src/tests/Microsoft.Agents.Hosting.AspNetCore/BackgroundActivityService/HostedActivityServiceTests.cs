// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundActivityService.Tests
{
    public class HostedActivityServiceTests
    {
        [Fact]
        public void Constructor_ShouldThrowWithNullConfig()
        {
            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(null, null, null, null, null));
        }

        [Fact]
        public void Constructor_ShouldThrowWithNullBot()
        {
            var config = new ConfigurationBuilder().Build();

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(config, null, null, null, null));
        }

        [Fact]
        public void Constructor_ShouldThrowWithNullAdapter()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(config, bot, null, null, null));
        }

        [Fact]
        public void Constructor_ShouldThrowWithNullActivityTaskQueue()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new TestAdapter();

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(config, bot, adapter, null, null));
        }

        [Fact]
        public void Constructor_ShouldInstantiateNullLogger()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new TestAdapter();
            var queue = new ActivityTaskQueue();

            new HostedActivityService(config, bot, adapter, queue, null);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessQueuedActivity()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new Mock<IChannelAdapter>();
            adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<BotCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Verifiable(Times.Once);
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<HostedActivityService>();

            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var queue = new ActivityTaskQueue();
            queue.QueueBackgroundActivity(claims, activity);

            var service = new HostedActivityService(config, bot, adapter.Object, queue, logger);

            var source = new CancellationTokenSource();
            // Start and stop the service, waiting for the activity to be processed.
            await service.StartAsync(source.Token).ContinueWith(async e => {
                await service.StopAsync(source.Token);
                Mock.Verify(adapter);
            });
        }


        [Fact]
        public async Task ExecuteAsync_ShouldLogErrorWhenProcessingQueuedActivity()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new Mock<IChannelAdapter>();
            adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<BotCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception())
                .Verifiable(Times.Once);
            var logger = new Mock<ILogger<HostedActivityService>>();
            logger.Setup(e => e.Log(LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Verifiable(Times.Once);

            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var queue = new ActivityTaskQueue();
            queue.QueueBackgroundActivity(claims, activity);

            var service = new HostedActivityService(config, bot, adapter.Object, queue, logger.Object);

            var source = new CancellationTokenSource();
            // Start and stop the service, waiting for the activity to be processed.
            await service.StartAsync(source.Token).ContinueWith(async e => {
                await service.StopAsync(source.Token);
                Mock.Verify(adapter, logger);
            });
        }

        [Fact]
        public void ExecuteAsync_ShouldCancelBackgroundProcess()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new Mock<IChannelAdapter>();
            adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<BotCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Verifiable(Times.Never);
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<HostedActivityService>();
            var queue = new ActivityTaskQueue();

            var service = new HostedActivityService(config, bot, adapter.Object, queue, logger);

            var source = new CancellationTokenSource();
            source.Cancel();
            var task = service.StartAsync(source.Token);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Mock.Verify(adapter);
        }
    }
}
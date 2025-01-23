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

namespace Microsoft.Agents.Hosting.AspNetCore.Tests.BackgroundActivityService
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

            _ = new HostedActivityService(config, bot, adapter, queue, null);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessQueuedActivity()
        {
            var record = UseRecord();
            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var source = new CancellationTokenSource();

            record.Adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<BotCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Verifiable(Times.Once);

            record.Queue.QueueBackgroundActivity(claims, activity);
            await record.Service.StartAsync(source.Token).ContinueWith(async e =>
            {
                // Start and stop the service, waiting for the activity to be processed.
                await record.Service.StopAsync(source.Token);
                record.VerifyMocks();
            });
        }


        [Fact]
        public async Task ExecuteAsync_ShouldLogErrorWhenProcessingQueuedActivity()
        {
            var record = UseRecord();
            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var source = new CancellationTokenSource();

            record.Adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<BotCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception())
                .Verifiable(Times.Once);
            record.Logger.Setup(e => e.Log(LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Verifiable(Times.Once);

            record.Queue.QueueBackgroundActivity(claims, activity);
            await record.Service.StartAsync(source.Token).ContinueWith(async e =>
            {
                // Start and stop the service, waiting for the activity to be processed.
                await record.Service.StopAsync(source.Token);
                record.VerifyMocks();
            });
        }

        [Fact]
        public void ExecuteAsync_ShouldCancelBackgroundProcess()
        {
            var record = UseRecord();
            var source = new CancellationTokenSource();

            record.Adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<BotCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Verifiable(Times.Never);

            source.Cancel();
            var task = record.Service.StartAsync(source.Token);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            record.VerifyMocks();
        }

        private static Record UseRecord()
        {
            var config = new ConfigurationBuilder().Build();
            var queue = new ActivityTaskQueue();
            var bot = new Mock<ActivityHandler>();
            var adapter = new Mock<IChannelAdapter>();
            var logger = new Mock<ILogger<HostedActivityService>>();

            var service = new HostedActivityService(config, bot.Object, adapter.Object, queue, logger.Object);
            return new(service, queue, bot, adapter, logger);
        }

        private record Record(
            HostedActivityService Service,
            ActivityTaskQueue Queue,
            Mock<ActivityHandler> Bot,
            Mock<IChannelAdapter> Adapter,
            Mock<ILogger<HostedActivityService>> Logger)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Bot, Adapter, Logger);
            }
        }
    }
}
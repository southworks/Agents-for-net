// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundTaskService.Tests
{
    public class HostedTaskServiceTests
    {
        [Fact]
        public void Constructor_ShouldThrowWithNullConfig()
        {
            Assert.Throws<ArgumentNullException>(() => new HostedTaskService(null, null, null));
        }

        [Fact]
        public void Constructor_ShouldThrowWithNullTaskQueue()
        {
            var config = new ConfigurationBuilder().Build();

            Assert.Throws<ArgumentNullException>(() => new HostedTaskService(config, null, null));
        }

        [Fact]
        public void Constructor_ShouldInstantiateNullLogger()
        {
            var config = new ConfigurationBuilder().Build();
            var queue = new BackgroundTaskQueue();

            new HostedTaskService(config, queue, null);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessQueuedActivity()
        {
            var config = new ConfigurationBuilder().Build();
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<HostedTaskService>();

            var queue = new BackgroundTaskQueue();
            var callback = new Mock<Func<CancellationToken, Task>>();
            callback.Setup(c => c(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once);
            queue.QueueBackgroundWorkItem(callback.Object);

            var service = new HostedTaskService(config, queue, logger);

            var source = new CancellationTokenSource();
            // Start and stop the service, waiting for the activity to be processed.
            await service.StartAsync(source.Token).ContinueWith(async e => {
                await service.StopAsync(source.Token);
                Mock.Verify(callback);
            });
        }


        [Fact]
        public async Task ExecuteAsync_ShouldLogErrorWhenProcessingQueuedActivity()
        {
            var config = new ConfigurationBuilder().Build();
            var logger = new Mock<ILogger<HostedTaskService>>();
            logger.Setup(e => e.Log(LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Verifiable(Times.Once);

            var queue = new BackgroundTaskQueue();
            var callback = new Mock<Func<CancellationToken, Task>>();
            callback.Setup(c => c(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception())
                .Verifiable(Times.Once);
            queue.QueueBackgroundWorkItem(callback.Object);

            var service = new HostedTaskService(config, queue, logger.Object);

            var source = new CancellationTokenSource();
            // Start and stop the service, waiting for the activity to be processed.
            await service.StartAsync(source.Token).ContinueWith(async e => {
                await service.StopAsync(source.Token);
                Mock.Verify(callback, logger);
            });
        }

        [Fact]
        public void ExecuteAsync_ShouldCancelBackgroundProcess()
        {
            var config = new ConfigurationBuilder().Build();
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<HostedTaskService>();

            var queue = new BackgroundTaskQueue();
            var callback = new Mock<Func<CancellationToken, Task>>();
            callback.Setup(c => c(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Never);
            queue.QueueBackgroundWorkItem(callback.Object);

            var service = new HostedTaskService(config, queue, logger);

            var source = new CancellationTokenSource();
            source.Cancel();
            var task = service.StartAsync(source.Token);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Mock.Verify(callback);
        }
    }
}
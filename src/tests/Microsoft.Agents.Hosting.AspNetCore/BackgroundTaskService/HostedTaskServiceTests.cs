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

namespace Microsoft.Agents.Hosting.AspNetCore.Tests.BackgroundTaskService
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

            _ = new HostedTaskService(config, queue, null);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessQueuedActivity()
        {
            var record = UseRecord();
            var callback = new Mock<Func<CancellationToken, Task>>();
            var source = new CancellationTokenSource();

            callback.Setup(c => c(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once);

            record.Queue.QueueBackgroundWorkItem(callback.Object);
            await record.Service.StartAsync(source.Token).ContinueWith(async e =>
            {
                // Start and stop the service, waiting for the activity to be processed.
                await record.Service.StopAsync(source.Token);
                Mock.Verify(callback);
                record.VerifyMocks();
            });
        }


        [Fact]
        public async Task ExecuteAsync_ShouldLogErrorWhenProcessingQueuedActivity()
        {
            var record = UseRecord();
            var callback = new Mock<Func<CancellationToken, Task>>();
            var source = new CancellationTokenSource();

            record.Logger.Setup(e => e.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Verifiable(Times.Once);
            callback.Setup(c => c(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception())
                .Verifiable(Times.Once);

            record.Queue.QueueBackgroundWorkItem(callback.Object);
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
            var callback = new Mock<Func<CancellationToken, Task>>();
            var queue = new BackgroundTaskQueue();
            var source = new CancellationTokenSource();

            callback.Setup(c => c(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Never);

            record.Queue.QueueBackgroundWorkItem(callback.Object);
            source.Cancel();
            var task = record.Service.StartAsync(source.Token);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Mock.Verify(callback);
            record.VerifyMocks();
        }

        private static Record UseRecord()
        {
            var config = new ConfigurationBuilder().Build();
            var queue = new BackgroundTaskQueue();
            var logger = new Mock<ILogger<HostedTaskService>>();

            var service = new HostedTaskService(config, queue, logger.Object);
            return new(service, queue, logger);
        }

        private record Record(
            HostedTaskService Service,
            BackgroundTaskQueue Queue,
            Mock<ILogger<HostedTaskService>> Logger)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Logger);
            }
        }
    }
}
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class HttpBotChannelFactoryTests
    {
        [Fact]
        public void Constructor_ShouldThrowOnNullHttpFactory()
        {
            var logger = new Mock<ILogger<HttpBotChannelFactory>>();

            Assert.Throws<ArgumentNullException>(() => new HttpBotChannelFactory(null, logger.Object));
        }

        [Fact]
        public void CreateChannel_ShouldReturnBotChannel()
        {
            var clientFactory = new Mock<IHttpClientFactory>();
            var logger = new Mock<ILogger<HttpBotChannelFactory>>();
            var provider = new Mock<IAccessTokenProvider>();
            var httpClient = new Mock<HttpClient>();

            clientFactory.Setup(e => e.CreateClient(It.IsAny<string>()))
                .Returns(httpClient.Object)
                .Verifiable(Times.Once);

            var channelFactory = new HttpBotChannelFactory(clientFactory.Object, logger.Object);

            var channel = channelFactory.CreateChannel(provider.Object);

            Assert.NotNull(channel);
            Mock.Verify(clientFactory);
        }
    }
}

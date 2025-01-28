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
        private readonly Mock<IHttpClientFactory> _clientFactory = new();
        private readonly Mock<ILogger<HttpBotChannelFactory>> _logger = new();
        private readonly Mock<IAccessTokenProvider> _provider = new();
        private readonly Mock<HttpClient> _httpClient = new();

        [Fact]
        public void Constructor_ShouldThrowOnNullHttpFactory()
        {
            Assert.Throws<ArgumentNullException>(() => new HttpBotChannelFactory(null, _logger.Object));
        }

        [Fact]
        public void CreateChannel_ShouldReturnBotChannel()
        {
            _clientFactory.Setup(e => e.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient.Object)
                .Verifiable(Times.Once);

            var channelFactory = new HttpBotChannelFactory(_clientFactory.Object, _logger.Object);

            var channel = channelFactory.CreateChannel(_provider.Object);

            Assert.NotNull(channel);
            Mock.Verify(_clientFactory);
        }
    }
}

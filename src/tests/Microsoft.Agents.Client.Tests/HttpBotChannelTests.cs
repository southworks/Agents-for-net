// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class HttpBotChannelTests
    {
        private readonly string _toBotId = "botid";
        private readonly string _toBotResource = "botresource";
        private readonly Uri _endpoint = new("http://endpoint");
        private readonly Uri _serviceUrl = new("http://serviceUrl");
        private readonly string _conversationId = "conversationid";
        private readonly Activity _activity = new(conversation: new());

        [Fact]
        public async Task PostActivityAsync_ShouldThrowOnNullEndpoint()
        {
            var provider = new Mock<IAccessTokenProvider>();
            var factory = new Mock<IHttpClientFactory>();
            var logger = new Mock<ILogger>();
            var channel = new HttpBotChannel(provider.Object, factory.Object, logger.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.PostActivityAsync(_toBotId, _toBotResource, null, _serviceUrl, _conversationId, _activity, CancellationToken.None));
        }

        [Fact]
        public async Task PostActivityAsync_ShouldThrowOnNullServiceUrl()
        {
            var provider = new Mock<IAccessTokenProvider>();
            var factory = new Mock<IHttpClientFactory>();
            var logger = new Mock<ILogger>();
            var channel = new HttpBotChannel(provider.Object, factory.Object, logger.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.PostActivityAsync(_toBotId, _toBotResource, _endpoint, null, _conversationId, _activity, CancellationToken.None));
        }

        [Fact]
        public async Task PostActivityAsync_ShouldThrowOnNullConversationId()
        {
            var provider = new Mock<IAccessTokenProvider>();
            var factory = new Mock<IHttpClientFactory>();
            var logger = new Mock<ILogger>();
            var channel = new HttpBotChannel(provider.Object, factory.Object, logger.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.PostActivityAsync(_toBotId, _toBotResource, _endpoint, _serviceUrl, null, _activity, CancellationToken.None));
        }

        [Fact]
        public async Task PostActivityAsync_ShouldThrowOnNullActivity()
        {
            var provider = new Mock<IAccessTokenProvider>();
            var factory = new Mock<IHttpClientFactory>();
            var logger = new Mock<ILogger>();
            var channel = new HttpBotChannel(provider.Object, factory.Object, logger.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.PostActivityAsync(_toBotId, _toBotResource, _endpoint, _serviceUrl, _conversationId, null, CancellationToken.None));
        }

        [Fact]
        public async Task PostActivityAsync_ShouldReturnSuccessfulInvokeResponse()
        {
            var provider = new Mock<IAccessTokenProvider>();
            var factory = new Mock<IHttpClientFactory>();
            var logger = new Mock<ILogger>();
            var httpClient = new Mock<HttpClient>();
            var content = "{\"text\": \"testing\"}";
            var message = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };

            provider.Setup(e => e.GetAccessTokenAsync(It.IsAny<string>(), It.Is<IList<string>>(e => e[0].StartsWith(_toBotId)), It.IsAny<bool>()))
                .ReturnsAsync("token")
                .Verifiable(Times.Once);
            factory.Setup(e => e.CreateClient(It.IsAny<string>()))
                .Returns(httpClient.Object)
                .Verifiable(Times.Once);
            httpClient.Setup(e => e.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(message)
                .Verifiable(Times.Once);

            var channel = new HttpBotChannel(provider.Object, factory.Object, logger.Object);
            var response = await channel.PostActivityAsync(_toBotId, _toBotResource, _endpoint, _serviceUrl, _conversationId, _activity, CancellationToken.None);

            Assert.Equal((int)message.StatusCode, response.Status);
            Assert.Equal(content, response.Body.ToString());
            Mock.Verify(provider, factory, httpClient);
        }

        [Fact]
        public async Task PostActivityAsync_ShouldReturnFailedInvokeResponse()
        {
            var provider = new Mock<IAccessTokenProvider>();
            var factory = new Mock<IHttpClientFactory>();
            var httpClient = new Mock<HttpClient>();
            var content = "{\"text\": \"testing\"}";
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(content) };

            provider.Setup(e => e.GetAccessTokenAsync(It.IsAny<string>(), It.Is<IList<string>>(e => e[0].StartsWith(_toBotId)), It.IsAny<bool>()))
                .ReturnsAsync("token")
                .Verifiable(Times.Once);
            factory.Setup(e => e.CreateClient(It.IsAny<string>()))
                .Returns(httpClient.Object)
                .Verifiable(Times.Once);
            httpClient.Setup(e => e.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(message)
                .Verifiable(Times.Once);

            var channel = new HttpBotChannel(provider.Object, factory.Object, null);
            var response = await channel.PostActivityAsync(_toBotId, _toBotResource, _endpoint, _serviceUrl, _conversationId, _activity, CancellationToken.None);

            Assert.Equal((int)message.StatusCode, response.Status);
            Assert.Equal(content, response.Body.ToString());
            Mock.Verify(provider, factory, httpClient);
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Microsoft.Agents.Protocols.Primitives;
using Xunit;

namespace Microsoft.Agents.Protocols.Connector.Tests
{
    public class UserTokenRestClientTests
    {
        private static readonly Uri Endpoint = new("http://localhost");
        private const string UserId = "user-id";
        private const string ConnectionName = "connection-name";
        private const string ChannelId = "channel-id";
        private const string Code = "code";
        private const string Include = "include";
        private readonly AadResourceUrls AadResourceUrls = new() { ResourceUrls = ["resource-url"] };
        private readonly TokenExchangeRequest TokenExchangeRequest = new();

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var client = UseClient();
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullEndpoint()
        {
            var pipeline = CreateHttpPipeline();
            Assert.Throws<UriFormatException>(() => new UserTokenRestClient(pipeline, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullHttpPipeline()
        {
            Assert.Throws<ArgumentNullException>(() => new UserTokenRestClient(null, Endpoint));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenAsync(null, ConnectionName, ChannelId));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenAsync(UserId, null, ChannelId));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowWithoutLocalBot()
        {
            var client = UseClient();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build does not throw an exception.
                await client.GetTokenAsync(UserId, ConnectionName, ChannelId, Code);
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.GetTokenAsync(UserId, ConnectionName, ChannelId, Code));
            }
        }

        [Fact]
        public async Task SignOutAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.SignOutAsync(null, ConnectionName, ChannelId));
        }

        [Fact]
        public async Task SignOutAsync_ShouldThrowWithoutLocalBot()
        {
            var client = UseClient();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build exception:
                await Assert.ThrowsAsync<ErrorResponseException>(() => client.SignOutAsync(UserId, ConnectionName, ChannelId));
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.SignOutAsync(UserId, ConnectionName, ChannelId));
            }
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenStatusAsync(null));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowWithoutLocalBot()
        {
            var client = UseClient();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build exception:
                await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetTokenStatusAsync(UserId, ChannelId, Include));
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.GetTokenStatusAsync(UserId, ChannelId, Include));
            }
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(null, ConnectionName, AadResourceUrls));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(UserId, null, AadResourceUrls));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowWithoutLocalBot()
        {
            var client = UseClient();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build exception:
                await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetAadTokensAsync(UserId, ConnectionName, AadResourceUrls, ChannelId));
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.GetAadTokensAsync(UserId, ConnectionName, AadResourceUrls, ChannelId));
            }
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsyncAsync(null, ConnectionName, ChannelId, TokenExchangeRequest));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsyncAsync(UserId, null, ChannelId, TokenExchangeRequest));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullChannelId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsyncAsync(UserId, ConnectionName, null, TokenExchangeRequest));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullExchangeRequest()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsyncAsync(UserId, ConnectionName, ChannelId, null));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowWithoutLocalBot()
        {
            var client = UseClient();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build exception:
                await client.ExchangeAsyncAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest);
                Assert.True(true, "No exception thrown.");
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.ExchangeAsyncAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest));
            }
        }

        private static HttpPipeline CreateHttpPipeline(int maxRetries = 0)
        {
            var options = new ConnectorClientOptions();
            options.Retry.MaxRetries = maxRetries;
            var pipeline = HttpPipelineBuilder.Build(options, new DefaultHeadersPolicy(options));
            return pipeline;
        }

        private static UserTokenRestClient UseClient()
        {
            return new UserTokenRestClient(CreateHttpPipeline(), Endpoint);
        }
    }
}

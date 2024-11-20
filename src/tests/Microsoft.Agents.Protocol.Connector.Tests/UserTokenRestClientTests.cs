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
        private readonly Uri Endpoint = new("http://localhost");

        [Fact]
        public void UserTokenRestClient_ShouldThrowOnNullBaseUri()
        {
            Assert.Throws<UriFormatException>(() =>
            {
                var pipeline = CreateHttpPipeline();
                return new UserTokenRestClient(pipeline, null);
            });
        }

        [Fact]
        public void UserTokenRestClient_ShouldNotThrowOnHttpUrl()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            Assert.NotNull(client);
        }

        [Fact]
        public void UserTokenRestClient_ShouldThrowOnNullHttpPipeline()
        {
            Assert.Throws<ArgumentNullException>(() => new UserTokenRestClient(null, Endpoint));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnEmptyUserId()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenAsync(null, "mockConnection", string.Empty));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnEmptyConnectionName()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenAsync("userid", null, string.Empty));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnNoLocalBot()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build does not throw an exception.
                await client.GetTokenAsync("dummyUserid", "dummyConnectionName", "dummyChannelId", "dummyCode");
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.GetTokenAsync(
                    "dummyUserid", "dummyConnectionName", "dummyChannelId", "dummyCode"));
            }
        }

        [Fact]
        public async Task SignOutAsync_ShouldThrowOnEmptyUserId()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.SignOutAsync(null, "dummyConnection"));
        }

        [Fact]
        public async Task SignOutAsync_ShouldThrowOnNoLocalBot()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build exception:
                await Assert.ThrowsAsync<ErrorResponseException>(() => client.SignOutAsync(
                    "dummyUserId", "dummyConnectionName", "dummyChannelId"));
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.SignOutAsync(
                    "dummyUserId", "dummyConnectionName", "dummyChannelId"));
            }
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnNullUserId()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenStatusAsync(null));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnNoLocalBot()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build exception:
                await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetTokenStatusAsync(
                    "dummyUserId", "dummyChannelId", "dummyInclude"));
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.GetTokenStatusAsync(
                    "dummyUserId", "dummyChannelId", "dummyInclude"));
            }
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullUserId()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(null, "connection", new AadResourceUrls() { ResourceUrls = new string[] { "hello" } }));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullConnectionName()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(
                "dummyUserId", null, new AadResourceUrls() { ResourceUrls = new string[] { "dummyUrl" } }));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNoLocalBot()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build exception:
                await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetAadTokensAsync(
                    "dummyUserId", "dummyConnectionName", new AadResourceUrls() { ResourceUrls = new string[] { "dummyUrl" } }, "dummyChannelId"));
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.GetAadTokensAsync(
                    "dummyUserId", "dummyConnectionName", new AadResourceUrls() { ResourceUrls = new string[] { "dummyUrl" } }, "dummyChannelId"));
            }
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullUserId()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsyncAsync(
                null, "dummyConnectionName", "dummyChannelId", new TokenExchangeRequest()));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullConnectionName()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsyncAsync(
                "dummyUserId", null, "dummyChannelId", new TokenExchangeRequest()));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullChannelId()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsyncAsync(
                "dummyUserId", "dummyConnectionName", null, new TokenExchangeRequest()));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullExchangeRequest()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsyncAsync(
                "dummyUserId", "dummyConnectionName", "dummyChannelId", null));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNoLocalBot()
        {
            var pipeline = CreateHttpPipeline();
            var client = new UserTokenRestClient(pipeline, Endpoint);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")) &&
                Environment.GetEnvironmentVariable("AGENT_OS").Equals("Windows_NT", StringComparison.Ordinal))
            {
                // Automated Windows build exception:
                await client.ExchangeAsyncAsync(
                    "dummyUserId", "dummyConnectionName", "dummyChannelId", new TokenExchangeRequest());
                Assert.True(true, "No exception thrown.");
            }
            else
            {
                // MacLinux build and local build exception:
                await Assert.ThrowsAsync<RequestFailedException>(() => client.ExchangeAsyncAsync(
                    "dummyUserId", "dummyConnectionName", "dummyChannelId", new TokenExchangeRequest()));
            }
        }

        private static HttpPipeline CreateHttpPipeline(int maxRetries = 0)
        {
            var options = new ConnectorClientOptions();
            options.Retry.MaxRetries = maxRetries;
            var pipeline = HttpPipelineBuilder.Build(options, new DefaultHeadersPolicy(options));
            return pipeline;
        }
    }
}

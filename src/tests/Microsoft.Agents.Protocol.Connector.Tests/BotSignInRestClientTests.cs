// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core.Pipeline;
using Azure;
using Microsoft.Agents.Protocols.Primitives;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Protocols.Connector.Tests
{
    public class BotSignInRestClientTests
    {
        private readonly Uri Endpoint = new("http://localhost");

        [Fact]
        public void BotSignInRestClient_ShouldThrowOnNullBaseUri()
        {
            Assert.Throws<UriFormatException>(() =>
            {
                var pipeline = CreateHttpPipeline();
                return new BotSignInRestClient(pipeline, null);
            });
        }

        [Fact]
        public void BotSignInRestClient_ShouldNotThrowOnHttpUrl()
        {
            var pipeline = CreateHttpPipeline();
            var client = new BotSignInRestClient(pipeline, Endpoint);
            Assert.NotNull(client);
        }

        [Fact]
        public void BotSignInRestClient_ShouldThrowOnNullHttpPipeline()
        {
            Assert.Throws<ArgumentNullException>(() => new BotSignInRestClient(null, Endpoint));
        }


        [Fact]
        public async Task GetSignInUrlAsync_ShouldThrowOnNullState()
        {
            var pipeline = CreateHttpPipeline();
            var client = new BotSignInRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInUrlAsync(null));
        }

        [Fact]
        public async Task GetSignInUrlAsync_ShouldThrowOnNoLocalBot()
        {
            var pipeline = CreateHttpPipeline();
            var client = new BotSignInRestClient(pipeline, Endpoint);

            await Assert.ThrowsAsync<RequestFailedException>(() => client.GetSignInUrlAsync(
                "dummyState", "dummyCodeChallenge", "dummyEmulatorUrl", "dummyFinalRedirect"));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowOnNullState()
        {
            var pipeline = CreateHttpPipeline();
            var client = new BotSignInRestClient(pipeline, Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInResourceAsync(
                null, null));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowOnNoLocalBot()
        {
            var pipeline = CreateHttpPipeline();
            var client = new BotSignInRestClient(pipeline, Endpoint);

            await Assert.ThrowsAsync<RequestFailedException>(() => client.GetSignInResourceAsync(
                "dummyState", "dummyCodeChallenge", "dummyEmulatorUrl", "dummyFinalRedirect"));
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

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
        private static readonly Uri Endpoint = new("http://localhost");
        private const string State = "state";
        private const string CodeCallenge = "code-challenge";
        private const string EmulatorUrl = "emulator-url";
        private const string FinalRedirect = "final-redirect";

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
            Assert.Throws<UriFormatException>(() => new BotSignInRestClient(pipeline, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullHttpPipeline()
        {
            Assert.Throws<ArgumentNullException>(() => new BotSignInRestClient(null, Endpoint));
        }


        [Fact]
        public async Task GetSignInUrlAsync_ShouldThrowOnNullState()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInUrlAsync(null));
        }

        [Fact]
        public async Task GetSignInUrlAsync_ShouldThrowWithoutLocalBot()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<RequestFailedException>(() => client.GetSignInUrlAsync(State, CodeCallenge, EmulatorUrl, FinalRedirect));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowOnNullState()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInResourceAsync(null, null));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowWithoutLocalBot()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<RequestFailedException>(() => client.GetSignInResourceAsync(State, CodeCallenge, EmulatorUrl, FinalRedirect));
        }

        private static HttpPipeline CreateHttpPipeline(int maxRetries = 0)
        {
            var options = new ConnectorClientOptions();
            options.Retry.MaxRetries = maxRetries;
            var pipeline = HttpPipelineBuilder.Build(options, new DefaultHeadersPolicy(options));
            return pipeline;
        }

        private static BotSignInRestClient UseClient()
        {
            return new BotSignInRestClient(CreateHttpPipeline(), Endpoint);
        }
    }
}

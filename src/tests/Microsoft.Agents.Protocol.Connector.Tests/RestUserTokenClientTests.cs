// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Protocols.Primitives;
using Xunit;
using Moq;
using Microsoft.Agents.Authentication;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.Agents.Protocols.Connector.Tests
{
    public class RestUserTokenClientTests
    {
        private const string AppId = "test-app-id";
        private const string Audience = "audience";
        private const string UserId = "user-id";
        private const string ConnectionName = "connection-name";
        private const string ChannelId = "channel-id";
        private const string MagicCode = "magic-code";
        private const string FinalRedirect = "final-redirect";
        private const string IncludeFilter = "include-filter";
        private static readonly Uri OauthEndpoint = new("https://test.endpoint");
        private static readonly List<string> Scopes = [];
        private readonly string[] ResourceUrls = ["https://test.url"];
        private static readonly Mock<IAccessTokenProvider> AccessTokenMock = new();
        private readonly TokenExchangeRequest TokenExchangeRequest = new();

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var client = UseClient();
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullAppId()
        {
            Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient(null, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null));
        }

        [Fact]
        public async Task GetUserTokenAsync_ShouldThrowOnDisposed()
        {
            var client = UseClient();
            client.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, MagicCode, CancellationToken.None));
        }

        [Fact]
        public async Task GetUserTokenAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserTokenAsync(null, ConnectionName, ChannelId, MagicCode, CancellationToken.None));
        }

        [Fact]
        public async Task GetUserTokenAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserTokenAsync(UserId, null, ChannelId, MagicCode, CancellationToken.None));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowOnDisposed()
        {
            var client = UseClient();
            client.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetSignInResourceAsync(ConnectionName, new Activity(), FinalRedirect, CancellationToken.None));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInResourceAsync(null, new Activity(), FinalRedirect, CancellationToken.None));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowOnNullActivity()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInResourceAsync(ConnectionName, null, FinalRedirect, CancellationToken.None));
        }

        [Fact]
        public async Task SignOutUserAsync_ShouldThrowOnDisposed()
        {
            var client = UseClient();
            client.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task SignOutUserAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.SignOutUserAsync(null, ConnectionName, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task SignOutUserAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.SignOutUserAsync(UserId, null, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnDisposed()
        {
            var client = UseClient();
            client.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetTokenStatusAsync(UserId, ConnectionName, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenStatusAsync(null, ChannelId, IncludeFilter, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnNullChannelId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenStatusAsync(UserId, null, IncludeFilter, CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnDisposed()
        {
            var client = UseClient();
            client.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetAadTokensAsync(UserId, ConnectionName, ResourceUrls, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(null, ConnectionName, ResourceUrls, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(UserId, null, ResourceUrls, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_ShouldThrowOnDisposed()
        {
            var client = UseClient();
            client.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(null, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(UserId, null, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public void Constructor_ShouldDisposeTwiceCorrectly()
        {
            var client = UseClient();
            client.Dispose();
            client.Dispose();
        }

        private static RestUserTokenClient UseClient()
        {
            return new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);
        }
    }
}

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
        private readonly Uri OauthEndpoint = new("https://test.endpoint");
        private readonly List<string> Scopes = [];
        private readonly Mock<IAccessTokenProvider> AccessTokenMock = new();

        [Fact]
        public void ConstructorShouldWork()
        {
            Assert.NotNull(new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null));
        }

        [Fact]
        public void ConstructorWithNullAppIdShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient(null, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null));
        }

        [Fact]
        public async Task GetUserTokenAsyncOfDisposedTokenShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            userToken.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await userToken.GetUserTokenAsync(UserId, ConnectionName, ChannelId, MagicCode, CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetUserTokenAsyncWithNullUserIdShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.GetUserTokenAsync(null, ConnectionName, ChannelId, MagicCode, CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetUserTokenAsyncWithNullConnectionNameShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.GetUserTokenAsync(UserId, null, ChannelId, MagicCode, CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetSignInResourceAsyncOfDisposedTokenShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            userToken.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await userToken.GetSignInResourceAsync(ConnectionName, new Activity(), "final-redirect", CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetSignInResourceAsyncWithNullConnectionNameShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.GetSignInResourceAsync(null, new Activity(), "final-redirect", CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetSignInResourceAsyncWithNullActivityShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.GetSignInResourceAsync(ConnectionName, null, "final-redirect", CancellationToken.None);
            });
        }

        [Fact]
        public async Task SignOutUserAsyncOfDisposedTokenShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            userToken.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await userToken.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);
            });
        }

        [Fact]
        public async Task SignOutUserAsyncWithNullUserIdShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.SignOutUserAsync(null, ConnectionName, ChannelId, CancellationToken.None);
            });
        }

        [Fact]
        public async Task SignOutUserAsyncWithNullConnectionNameShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.SignOutUserAsync(UserId, null, ChannelId, CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetTokenStatusAsyncOfDisposedTokenShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            userToken.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await userToken.GetTokenStatusAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetTokenStatusAsyncWithNullUserIdShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.GetTokenStatusAsync(null, ChannelId, "filter", CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetTokenStatusAsyncWithNullChannelIdShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.GetTokenStatusAsync(UserId, null, "filter", CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetAadTokensAsyncOfDisposedTokenShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            string[] resourceUrls = { "https://test.url" };

            userToken.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await userToken.GetAadTokensAsync(UserId, ConnectionName, resourceUrls, ChannelId, CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetAadTokensAsyncWithNullUserIdShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            string[] resourceUrls = { "https://test.url" };

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.GetAadTokensAsync(null, ChannelId, resourceUrls, ChannelId, CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetAadTokensAsyncWithNullConnectionNameShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            string[] resourceUrls = { "https://test.url" };

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.GetAadTokensAsync(UserId, null, resourceUrls, ChannelId, CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExchangeTokenAsyncOfDisposedTokenShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            var tokenExchange = new TokenExchangeRequest();

            userToken.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await userToken.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, tokenExchange, CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExchangeTokenAsyncWithNullUserIdShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            var tokenExchange = new TokenExchangeRequest();

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.ExchangeTokenAsync(null, ChannelId, ChannelId, tokenExchange, CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExchangeTokenAsyncWithNullConnectionNameShouldThrow()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);

            var tokenExchange = new TokenExchangeRequest();

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await userToken.ExchangeTokenAsync(UserId, null, ChannelId, tokenExchange, CancellationToken.None);
            });
        }

        [Fact]
        public void DisposeOfDisposedTokenShouldReturn()
        {
            var userToken = new RestUserTokenClient(AppId, OauthEndpoint, AccessTokenMock.Object, Audience, Scopes, null);
            userToken.Dispose();
            userToken.Dispose();
        }
    }
}

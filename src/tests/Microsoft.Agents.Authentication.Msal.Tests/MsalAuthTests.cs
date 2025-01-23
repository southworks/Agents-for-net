// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Castle.Core.Logging;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Teams.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.LoggingExtensions;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class MsalAuthTests
    {
        private static readonly Mock<IServiceProvider> service = new Mock<IServiceProvider>();

        private static readonly Dictionary<string, string> configSettings = new Dictionary<string, string> {
            { "Connections:BotServiceConnection:Settings:AuthType", "ClientSecret" },
            { "Connections:BotServiceConnection:Settings:ClientId", "test-id" },
            { "Connections:BotServiceConnection:Settings:ClientSecret", "test-secret" },
            { "Connections:BotServiceConnection:Settings:TenantId", "test-tenant" },
        };
        private const string SettingsSection = "Connections:BotServiceConnection:Settings";

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configSettings)
            .Build();

        //private readonly Mock<IServiceProvider> _serviceProviderMock;
        //private readonly Mock<IConfigurationSection> _configurationSectionMock;
        //private static readonly Mock<ILogger<MsalAuth>> loggerMock;
        private readonly Mock<ICertificateProvider> _certificateProviderMock;
        private readonly Mock<IConfidentialClientApplication> _confidentialClientMock;
        private readonly Mock<IManagedIdentityApplication> _managedIdentityClientMock;
        //private readonly MsalAuth _msalAuth;

        public MsalAuthTests()
        {
            //_serviceProviderMock = new Mock<IServiceProvider>();
            //_configurationSectionMock = new Mock<IConfigurationSection>();
            //_loggerMock = new Mock<ILogger<MsalAuth>>();
            _certificateProviderMock = new Mock<ICertificateProvider>();
            _confidentialClientMock = new Mock<IConfidentialClientApplication>();
            _managedIdentityClientMock = new Mock<IManagedIdentityApplication>();

            //_serviceProviderMock.Setup(sp => sp.GetService(typeof(ILogger<MsalAuth>))).Returns(_loggerMock.Object);
            //_serviceProviderMock.Setup(sp => sp.GetService(typeof(ICertificateProvider))).Returns(_certificateProviderMock.Object);

            //_msalAuth = new MsalAuth(_serviceProviderMock.Object, _configurationSectionMock.Object);
        }


        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var msal = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            Assert.NotNull(msal);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new MsalAuth(null, configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new MsalAuth(service.Object, null));
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldThrowOnMalformedUri()
        {
            var msal = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            await Assert.ThrowsAsync<ArgumentException>(() => msal.GetAccessTokenAsync(null, [], false));
        }

        //[Fact]
        //public async Task GetAccessTokenAsync_ShouldReturnTokenForClientCredentials()
        //{
        //    IList<string> Scopes = new List<string> { "https://api.botframework.com/.default" };
        //    string resourceUrl = "https://test.url";

        //    var mockOptions = new Mock<IOptions<MsalAuthConfigurationOptions>>();

        //    var returnedOptions = new MsalAuthConfigurationOptions
        //    {
        //        MSALEnabledLogPII = false
        //    };
        //    mockOptions.Setup(x => x.Value).Returns(returnedOptions);

        //    var logger = new Mock<ILogger<MsalAuth>>();

        //    var service = new Mock<IServiceProvider>();
        //    service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(mockOptions.Object);
        //    service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);
        //    service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);

        //    var msal = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

        //    var token = await msal.GetAccessTokenAsync(resourceUrl, Scopes, false);

        //    Assert.NotNull(token);
        //}

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenFromCache()
        {
            var resourceUrl = "https://example.com";
            var scopes = new List<string> { "scope1" };
            var token = "valid_token";
            var authResult = new AuthenticationResult(token, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());
            var authResults = new ExecuteAuthenticationResults { MsalAuthResult = authResult };
            var cacheList = new ConcurrentDictionary<Uri, ExecuteAuthenticationResults>();
            cacheList.TryAdd(new Uri(resourceUrl), authResults);

            var msalAuth = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            // Use reflection to set the private _cacheList property
            var cacheListField = typeof(MsalAuth).GetField("_cacheList", BindingFlags.NonPublic | BindingFlags.Instance);
            cacheListField.SetValue(msalAuth, cacheList);

            var result = await msalAuth.GetAccessTokenAsync(resourceUrl, scopes);

            Assert.Equal(token, result);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldRefreshTokenWhenExpired()
        {
            var resourceUrl = "https://example.com";
            var scopes = new List<string> { "scope1" };
            var expiredToken = "expired_token";
            var newToken = "new_token";
            var expiredAuthResult = new AuthenticationResult(expiredToken, false, null, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null, Guid.NewGuid());
            var newAuthResult = new AuthenticationResult(newToken, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());
            var expiredAuthResults = new ExecuteAuthenticationResults { MsalAuthResult = expiredAuthResult };
            var cacheList = new ConcurrentDictionary<Uri, ExecuteAuthenticationResults>();
            cacheList.TryAdd(new Uri(resourceUrl), expiredAuthResults);

            var mockOptions = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            mockOptions.Setup(x => x.Value).Returns(returnedOptions);

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();

            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(mockOptions.Object);
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory))).Returns(new TestHttpClientFactory());

            var msalAuth = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));
            
            // Use reflection to set the private _cacheList property
            var cacheListField = typeof(MsalAuth).GetField("_cacheList", BindingFlags.NonPublic | BindingFlags.Instance);
            cacheListField.SetValue(msalAuth, cacheList);

            var result = await msalAuth.GetAccessTokenAsync(resourceUrl, scopes);

            Assert.Equal(newToken, result);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenForClientCredentials()
        {
            var resourceUrl = "https://example.com";
            var scopes = new List<string> { "scope1" };
            var newToken = "new_token";
            var newAuthResult = new AuthenticationResult(newToken, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());

            var mockOptions = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            mockOptions.Setup(x => x.Value).Returns(returnedOptions);

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();

            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(mockOptions.Object);
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory))).Returns(new TestHttpClientFactory());

            var msalAuth = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            var result = await msalAuth.GetAccessTokenAsync(resourceUrl, scopes, true);

            Assert.Equal(newToken, result);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenForManagedIdentity()
        {
            var configSettings = new Dictionary<string, string> {
                { "Connections:BotServiceConnection:Settings:AuthType", "UserManagedIdentity" },
                { "Connections:BotServiceConnection:Settings:ClientId", "test-id" },
                { "Connections:BotServiceConnection:Settings:TenantId", "test-tenant" },
            };
            var SettingsSection = "Connections:BotServiceConnection:Settings";

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();
        
            var resourceUrl = "https://example.com";
            var scopes = new List<string> { "scope1" };
            var newToken = "new_token";
            var newAuthResult = new AuthenticationResult(newToken, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());

            var mockOptions = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            mockOptions.Setup(x => x.Value).Returns(returnedOptions);

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();

            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(mockOptions.Object);
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory))).Returns(new TestHttpClientFactory());

            var msalAuth = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            var result = await msalAuth.GetAccessTokenAsync(resourceUrl, scopes, true);

            Assert.Equal(newToken, result);
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                var response = new
                {
                    Access_token = "new_token", //"MTQ0NjJkZmQ5OTM2NDE1ZTZjNGZmZjI3",
                    Token_type = "Bearer",
                    Expires_in = 3600,
                    //Refresh_token = "IwOGYzYTlmM2YxOTQ5MGE3YmNmMDFkNTVk",
                    Scope = "create"
                };
                var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ProtocolJsonSerializer.ToJson(response))
                };

                var client = new Mock<HttpClient>();
                client.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);
                return client.Object;
            }
        }
    }
}

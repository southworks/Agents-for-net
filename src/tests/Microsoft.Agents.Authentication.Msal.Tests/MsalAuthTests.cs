// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
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

        private readonly Mock<ICertificateProvider> _certificateProviderMock;
        private readonly Mock<IConfidentialClientApplication> _confidentialClientMock;
        private readonly Mock<IManagedIdentityApplication> _managedIdentityClientMock;

        public MsalAuthTests()
        {
            _certificateProviderMock = new Mock<ICertificateProvider>();
            _confidentialClientMock = new Mock<IConfidentialClientApplication>();
            _managedIdentityClientMock = new Mock<IManagedIdentityApplication>();
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
            //var newAuthResult = new AuthenticationResult(newToken, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());

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
        public async Task GetAccessTokenAsync_ShouldReturnTokenForCertificate()
        {
            var newToken = "new_token";
            //var scopes = new List<string>() { "{instance}", "scope1" }.ToString();
            var configSettings = new Dictionary<string, string> {
                { "Connections:BotServiceConnection:Settings:AuthType", "Certificate" },
                { "Connections:BotServiceConnection:Settings:ClientId", "test-id" },
                { "Connections:BotServiceConnection:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:BotServiceConnection:Settings:CertThumbprint", "thumbprint" },
                { "Connections:BotServiceConnection:Settings:Scopes:scope1", "{instance}" }

            };
            var SettingsSection = "Connections:BotServiceConnection:Settings";

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var resourceUrl = "https://example.com";
            //var scopes = new List<string> { "scope1" };
            var mockOptions = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            mockOptions.Setup(x => x.Value).Returns(returnedOptions);

            var logger = new Mock<ILogger<MsalAuth>>();

            var mockCertificate = new Mock<ICertificateProvider>();
            mockCertificate.Setup(x => x.GetCertificate()).Returns(CreateSelfSignedCertificate("test"));

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(mockOptions.Object);
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory))).Returns(new TestHttpClientFactory());
            service.Setup(sp => sp.GetService(typeof(ICertificateProvider))).Returns(mockCertificate.Object);

            var msalAuth = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            var result = await msalAuth.GetAccessTokenAsync(resourceUrl, [], true);

            Assert.Equal(newToken, result);
        }

        //[Fact]
        //public async Task GetAccessTokenAsync_ShouldReturnTokenForManagedIdentity()
        //{
        //    var configSettings = new Dictionary<string, string> {
        //        { "Connections:BotServiceConnection:Settings:AuthType", "SystemManagedIdentity" },
        //        { "Connections:BotServiceConnection:Settings:ClientId", "test-id" },
        //        { "Connections:BotServiceConnection:Settings:TenantId", "test-tenant" },
        //    };
        //    var SettingsSection = "Connections:BotServiceConnection:Settings";

        //    IConfiguration configuration = new ConfigurationBuilder()
        //        .AddInMemoryCollection(configSettings)
        //        .Build();
        
        //    var resourceUrl = "https://example.com";
        //    var scopes = new List<string> { "scope1" };
        //    var newToken = "new_token";
        //    //var newAuthResult = new AuthenticationResult(newToken, false, new Guid().ToString(), DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), "tenant-id", null, "token-id", [], Guid.NewGuid());

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
        //    service.Setup(sp => sp.GetService(typeof(IHttpClientFactory))).Returns(new TestHttpClientFactory());
        //    service.Setup(sp => sp.GetService(typeof(IManagedIdentityApplication))).Returns(new TestManagedIdentityApplication());

        //    var msalAuth = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

        //    var result = await msalAuth.GetAccessTokenAsync(resourceUrl, scopes, true);

        //    Assert.Equal(newToken, result);
        //}


        private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));

            return certificate;
        }

        private class TestManagedIdentityApplication : IManagedIdentityApplication
        {
            public AcquireTokenForManagedIdentityParameterBuilder AcquireTokenForManagedIdentity(string resource)
            {
                var mockBuilder = new Mock<AcquireTokenForManagedIdentityParameterBuilder>();
                var expectedToken = "mocked_token";

                mockBuilder
                    .Setup(builder => builder.ExecuteAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AuthenticationResult(expectedToken, false, null, DateTimeOffset.Now, DateTimeOffset.Now.AddHours(1), null, null, null, null, new Guid()));

                return mockBuilder.Object;
            }
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                var response = new
                {
                    Headers = new { UserAgent = "user-agent" },
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

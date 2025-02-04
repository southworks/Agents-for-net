﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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
        private static readonly Mock<IServiceProvider> _service = new Mock<IServiceProvider>();

        private static readonly Dictionary<string, string> _configSettings = new Dictionary<string, string> {
            { "Connections:BotServiceConnection:Settings:AuthType", "ClientSecret" },
            { "Connections:BotServiceConnection:Settings:ClientId", "test-id" },
            { "Connections:BotServiceConnection:Settings:ClientSecret", "test-secret" },
            { "Connections:BotServiceConnection:Settings:TenantId", "test-tenant" },
        };
        private const string SettingsSection = "Connections:BotServiceConnection:Settings";

        IConfiguration _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(_configSettings)
            .Build();

        private const string ResourceUrl = "https://example.com";
        private readonly List<string>  _scopes = ["scope1"];

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var msal = new MsalAuth(_service.Object, _configuration.GetSection(SettingsSection));

            Assert.NotNull(msal);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new MsalAuth(null, _configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new MsalAuth(_service.Object, null));
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldThrowOnMalformedUri()
        {
            var msalAuth = new MsalAuth(_service.Object, _configuration.GetSection(SettingsSection));

            await Assert.ThrowsAsync<ArgumentException>(() => msalAuth.GetAccessTokenAsync(null, _scopes, false));
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldThrowOnNullScopesForClientCredentials()
        {
            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);

            var msalAuth = new MsalAuth(service.Object, _configuration.GetSection(SettingsSection));

            await Assert.ThrowsAsync<ArgumentException>(() => msalAuth.GetAccessTokenAsync(ResourceUrl, null, false));
            Mock.Verify(options, service);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenFromCache()
        {
            var token = "valid_token";
            var authResult = new AuthenticationResult(token, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());
            var authResults = new ExecuteAuthenticationResults { MsalAuthResult = authResult };
            var cacheList = new ConcurrentDictionary<Uri, ExecuteAuthenticationResults>();
            cacheList.TryAdd(new Uri(ResourceUrl), authResults);

            var msalAuth = new MsalAuth(_service.Object, _configuration.GetSection(SettingsSection));

            // Use reflection to set the private _cacheList property
            var cacheListField = typeof(MsalAuth).GetField("_cacheList", BindingFlags.NonPublic | BindingFlags.Instance);
            cacheListField.SetValue(msalAuth, cacheList);

            var result = await msalAuth.GetAccessTokenAsync(ResourceUrl, _scopes);

            Assert.Equal(token, result);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldRefreshTokenWhenExpired()
        {
            var expiredToken = "expired_token";
            var newToken = "token";
            var expiredAuthResult = new AuthenticationResult(expiredToken, false, null, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null, Guid.NewGuid());
            var newAuthResult = new AuthenticationResult(newToken, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());
            var expiredAuthResults = new ExecuteAuthenticationResults { MsalAuthResult = expiredAuthResult };
            var cacheList = new ConcurrentDictionary<Uri, ExecuteAuthenticationResults>();
            cacheList.TryAdd(new Uri(ResourceUrl), expiredAuthResults);

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory())
                .Verifiable(Times.Exactly(2));

            var msalAuth = new MsalAuth(service.Object, _configuration.GetSection(SettingsSection));
            
            // Use reflection to set the private _cacheList property
            var cacheListField = typeof(MsalAuth).GetField("_cacheList", BindingFlags.NonPublic | BindingFlags.Instance);
            cacheListField.SetValue(msalAuth, cacheList);

            var result = await msalAuth.GetAccessTokenAsync(ResourceUrl, _scopes);

            Assert.Equal(newToken, result);
            Mock.Verify(options, service);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenForClientCredentials()
        {
            var token = "token";

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory())
                .Verifiable(Times.Once);

            var msalAuth = new MsalAuth(service.Object, _configuration.GetSection(SettingsSection));

            var result = await msalAuth.GetAccessTokenAsync(ResourceUrl, _scopes, true);

            Assert.Equal(token, result);
            Mock.Verify(options, service);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenForCertificate()
        {
            var token = "token";

            var configSettings = new Dictionary<string, string> {
                { "Connections:BotServiceConnection:Settings:AuthType", "Certificate" },
                { "Connections:BotServiceConnection:Settings:ClientId", "test-id" },
                { "Connections:BotServiceConnection:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:BotServiceConnection:Settings:CertThumbprint", "thumbprint" },
                { "Connections:BotServiceConnection:Settings:Scopes:scope1", "{instance}" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var certificate = new Mock<ICertificateProvider>();
            certificate.Setup(x => x.GetCertificate())
                .Returns(CreateSelfSignedCertificate("test"))
                .Verifiable(Times.Once);

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory())
                .Verifiable(Times.Exactly(2));
            service.Setup(sp => sp.GetService(typeof(ICertificateProvider)))
                .Returns(certificate.Object)
                .Verifiable(Times.Once);

            var msalAuth = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            var result = await msalAuth.GetAccessTokenAsync(ResourceUrl, [], true);

            Assert.Equal(token, result);
            Mock.Verify(options, service, certificate);
        }

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

        private class TestHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                var response = new
                {
                    Access_token = "token",
                    Token_type = "Bearer",
                    Expires_in = 3600,
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class MsalAuthTests
    {
        private readonly Mock<IServiceProvider> service = new Mock<IServiceProvider>();

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
        public async Task GetAccessTokenAsync_ShouldReturnTokenForClientCredentials()
        {
            IList<string> Scopes = new List<string> { "https://api.botframework.com/.default" };
            string resourceUrl = "https://test.url";

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
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);

            var msal = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            var token = await msal.GetAccessTokenAsync(resourceUrl, Scopes, false);

            Assert.NotNull(token);
        }
    }
}

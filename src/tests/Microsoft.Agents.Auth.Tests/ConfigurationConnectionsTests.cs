using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class ConfigurationConnectionsTests
    {
        private readonly ConfigurationConnections _configurationConnections;
        private readonly ConfigurationRoot _config;

        public ConfigurationConnectionsTests()
        {
            _config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "BotServiceConnection" },
                        { "Connections:BotServiceConnection:Type", "MsalAuth" },
                        { "Connections:BotServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:BotServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:BotServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:BotServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            _configurationConnections = new ConfigurationConnections(serviceProvider.Object, _config);
        }

        [Fact]
        public void GetConnection_ShouldReturnMsalAuthObject()
        {
            //Act
            var response = _configurationConnections.GetConnection("BotServiceConnection");

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetDefaultConnection_ShouldReturnMsalAuthObjectFromMap()
        {
            //Act
            var response = _configurationConnections.GetDefaultConnection();

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetDefaultConnection_ShouldReturnMsalAuthObjectFromConnections()
        {
            //Arrange
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "BotServiceConnection" },
                        { "Connections:BotServiceConnection:Type", "MsalAuth" },
                        { "Connections:BotServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:BotServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:BotServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:BotServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var configurationConnections = new ConfigurationConnections(serviceProvider.Object, config);

            //Act
            var response = configurationConnections.GetDefaultConnection();

            //Assert
            Assert.NotNull(response);
        }
    }
}

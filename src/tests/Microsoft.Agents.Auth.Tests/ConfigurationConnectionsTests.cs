using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Agents.Auth.Tests
{
    public class ConfigurationConnectionsTests
    {
        [Fact]
        public void GetConnection_ShouldReturnAccessTokenProviderWithConnectionName()
        {
            //Arrange
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
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
            var configurationConnections = new ConfigurationConnections(serviceProvider.Object, config);

            //Act
            var response = configurationConnections.GetConnection("BotServiceConnection");

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetDefaultConnection_ShouldReturnAccessTokenProviderFromMap()
        {
            //Arrange
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
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
            var configurationConnections = new ConfigurationConnections(serviceProvider.Object, config);

            //Act
            var response = configurationConnections.GetDefaultConnection();

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetDefaultConnection_ShouldReturnAccessTokenProviderFromConnections()
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

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnMatchingServiceUrl()
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
            ClaimsIdentity identity = new ClaimsIdentity(new List<Claim>());

            //Act
            var response = configurationConnections.GetTokenProvider(identity, "serviceUrl");

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnEmptyServiceUrl()
        {
            //Arrange
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
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
            ClaimsIdentity identity = new(new List<Claim>());

            //Act
            var response = configurationConnections.GetTokenProvider(identity, "serviceUrl");

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnGenericServiceUrl()
        {
            //Arrange
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
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
            var configurationConnections = new ConfigurationConnections(serviceProvider.Object, config);
            ClaimsIdentity identity = new(new List<Claim>());

            //Act
            var response = configurationConnections.GetTokenProvider(identity, "generic");

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderFromConnectionInstance()
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
            ClaimsIdentity identity = new ClaimsIdentity(new List<Claim>());

            //Act
            var response = configurationConnections.GetTokenProvider(identity, "serviceUrl");
            //Call a second time to obtain AccessTokenProvider from the Connection instance
            response = configurationConnections.GetTokenProvider(identity, "serviceUrl");

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnNullOnNotMatchingServiceUrl()
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
            ClaimsIdentity identity = new(new List<Claim>());

            //Act
            var response = configurationConnections.GetTokenProvider(identity, "noUrl");

            //Assert
            Assert.Null(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnNullOnEmptyConnections()
        {
            //Arrange
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "BotServiceConnection" },
                        { "ConnectionsMap:0:Audience", "audience" }
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var configurationConnections = new ConfigurationConnections(serviceProvider.Object, config);
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AudienceClaim, "audience"),
            };
            ClaimsIdentity identity = new(claims);
            
            //Act
            var response = configurationConnections.GetTokenProvider(identity, "serviceUrl");

            //Assert
            Assert.Null(response);
        }

        [Fact]
        public void GetProviderConstructor_ShouldReturnConstructorInfoOnValidProviderType()
        {
            //Arrange
            var assemblyLoader = new AssemblyLoader(AssemblyLoadContext.Default);

            //Act
            var response = assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", "MsalAuth");

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetProviderConstructor_ShouldReturnConstructorInfoOnNullType()
        {
            //Arrange
            var assemblyLoader = new AssemblyLoader(AssemblyLoadContext.Default);

            //Act
            var response = assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", null);

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnNullLoadContext()
        {
            //Assert
            Assert.Throws<ArgumentNullException>(() => new AssemblyLoader(null));
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnNullAssemblyName()
        {
            //Arrange
            var assemblyLoader = new AssemblyLoader(AssemblyLoadContext.Default);

            //Assert
            Assert.Throws<ArgumentNullException>(() => assemblyLoader.GetProviderConstructor("name", null, "type-name"));
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnInvalidProviderType()
        {
            //Arrange
            var assemblyLoader = new AssemblyLoader(AssemblyLoadContext.Default);

            //Assert
            Assert.Throws<InvalidOperationException>(() => assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", "type"));
        }
    }
}

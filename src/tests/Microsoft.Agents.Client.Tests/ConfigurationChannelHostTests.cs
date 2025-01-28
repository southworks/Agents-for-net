// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class ConfigurationChannelHostTests
    {
        private readonly string _defaultChannel = "webchat";

        [Fact]
        public void Constructor_ShouldThrowOnNullConfigSection()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new Mock<IConfiguration>();

            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(provider.Object, connections.Object, config.Object, _defaultChannel, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnEmptyConfigSection()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new Mock<IConfiguration>();

            Assert.Throws<ArgumentException>(() => new ConfigurationChannelHost(provider.Object, connections.Object, config.Object, _defaultChannel, string.Empty));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new Mock<IConfiguration>();

            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(null, connections.Object, config.Object, _defaultChannel));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnections()
        {
            var provider = new Mock<IServiceProvider>();
            var config = new Mock<IConfiguration>();

            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(provider.Object, null, config.Object, _defaultChannel));
        }

        [Fact]
        public void Constructor_ShouldSetChannel()
        {
            var botId = "bot1";
            var channel = "testing";
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var sections = new Dictionary<string, string>{
                {"ChannelHost:Channels:0:Id", botId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, channel);

            Assert.Single(host.Channels);
            Assert.Equal(botId, host.Channels[botId].Id);
            Assert.Equal(channel, host.Channels[botId].ChannelFactory);
        }

        [Fact]
        public void Constructor_ShouldSetHostEndpoint()
        {
            var endpoint = "http://localhost/";
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var sections = new Dictionary<string, string>{
                {"ChannelHost:HostEndpoint", endpoint},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);

            Assert.Equal(endpoint, host.HostEndpoint.ToString());
        }

        [Fact]
        public void Constructor_ShouldSetHostAppId()
        {
            var appId = "123";
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var sections = new Dictionary<string, string>{
                {"ChannelHost:HostAppId", appId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);

            Assert.Equal(appId, host.HostAppId);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullName()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty ?? null));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyName()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnUnknownChannel()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel("random"));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannel()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);
            host.Channels.Add(_defaultChannel, null);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannelFactory()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();
            var channelInfo = new Mock<IChannelInfo>();

            channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns(() => null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);
            host.Channels.Add(_defaultChannel, channelInfo.Object);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(channelInfo);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelFactory()
        {
            var provider = new Mock<IServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();
            var channelInfo = new Mock<IChannelInfo>();

            channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns(string.Empty)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);
            host.Channels.Add(_defaultChannel, channelInfo.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(channelInfo);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullKeyedService()
        {
            var provider = new Mock<IKeyedServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();
            var channelInfo = new Mock<IChannelInfo>();

            channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns<object>(null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);
            host.Channels.Add(_defaultChannel, channelInfo.Object);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(provider);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannelTokenProvider()
        {
            var provider = new Mock<IKeyedServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();
            var channelInfo = new Mock<IChannelInfo>();
            var channelFactory = new Mock<IChannelFactory>();

            channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            channelInfo.SetupGet(e => e.TokenProvider)
                .Returns(() => null)
                .Verifiable(Times.Once);
            provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(channelFactory.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);
            host.Channels.Add(_defaultChannel, channelInfo.Object);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(channelInfo, provider);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelTokenProvider()
        {
            var provider = new Mock<IKeyedServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();
            var channelInfo = new Mock<IChannelInfo>();
            var channelFactory = new Mock<IChannelFactory>();

            channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            channelInfo.SetupGet(e => e.TokenProvider)
                .Returns(string.Empty)
                .Verifiable(Times.Once);
            provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(channelFactory.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);
            host.Channels.Add(_defaultChannel, channelInfo.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(channelInfo, provider);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullConnection()
        {
            var provider = new Mock<IKeyedServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();
            var channelInfo = new Mock<IChannelInfo>();
            var channelFactory = new Mock<IChannelFactory>();

            channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            channelInfo.SetupGet(e => e.TokenProvider)
                .Returns("provider")
                .Verifiable(Times.Exactly(2));
            provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(channelFactory.Object)
                .Verifiable(Times.Once);
            connections.Setup(e => e.GetConnection(It.IsAny<string>()))
                .Returns<IAccessTokenProvider>(null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);
            host.Channels.Add(_defaultChannel, channelInfo.Object);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(channelInfo, provider, connections);
        }

        [Fact]
        public void GetChannel_ShouldReturnChannel()
        {
            var provider = new Mock<IKeyedServiceProvider>();
            var connections = new Mock<IConnections>();
            var config = new ConfigurationBuilder().Build();
            var channelInfo = new Mock<IChannelInfo>();
            var channelFactory = new Mock<IChannelFactory>();
            var token = new Mock<IAccessTokenProvider>();
            var channel = new Mock<IChannel>();

            channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            channelInfo.SetupGet(e => e.TokenProvider)
                .Returns("provider")
                .Verifiable(Times.Exactly(2));
            provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(channelFactory.Object)
                .Verifiable(Times.Once);
            connections.Setup(e => e.GetConnection(It.IsAny<string>()))
                .Returns(token.Object)
                .Verifiable(Times.Once);
            channelFactory.Setup(e => e.CreateChannel(It.IsAny<IAccessTokenProvider>()))
                .Returns(channel.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(provider.Object, connections.Object, config, _defaultChannel);
            host.Channels.Add(_defaultChannel, channelInfo.Object);
            var result = host.GetChannel(_defaultChannel);

            Assert.Equal(channel.Object, result);
            Mock.Verify(channelInfo, provider, connections, channelFactory);
        }
    }
}

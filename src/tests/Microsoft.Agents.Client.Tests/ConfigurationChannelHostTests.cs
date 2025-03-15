// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class ConfigurationChannelHostTests
    {
        private readonly string _defaultChannel = "webchat";
        private readonly Mock<IKeyedServiceProvider> _provider = new();
        private readonly Mock<IConnections> _connections = new();
        private readonly IConfigurationRoot _config = new ConfigurationBuilder().Build();
        private readonly Mock<HttpBotChannelSettings> _channelInfo = new();
        private readonly Mock<IAccessTokenProvider> _token = new();
        private readonly Mock<IChannel> _channel = new();
        private readonly Mock<IConversationIdFactory> _conversationIdFactory = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactory = new();

        [Fact]
        public void Constructor_ShouldThrowOnNullConfigSection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnEmptyConfigSection()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object, ""));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, null, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnections()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, null, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldSetProperties()
        {
            var botAlias = "bot1";
            var botClientId = "123";
            var botTokenProvider = "BotServiceConnection";
            var botEndpoint = "http://localhost/api/messages";
            var defaultHostEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {"ChannelHost:Channels:0:Alias", botAlias},
                {"ChannelHost:Channels:0:ConnectionSettings:ClientId", botClientId},
                {"ChannelHost:Channels:0:ConnectionSettings:TokenProvider", botTokenProvider},
                {"ChannelHost:Channels:0:ConnectionSettings:Endpoint", botEndpoint},
                {"ChannelHost:DefaultHostEndpoint", defaultHostEndpoint},
                {"ChannelHost:HostClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);

            Assert.Single(host.Channels);
            Assert.Equal(botAlias, host.Channels[botAlias].Alias);
            Assert.Equal(botClientId, host.Channels[botAlias].ConnectionSettings.ClientId);
            Assert.Equal(defaultHostEndpoint, host.DefaultHostEndpoint.ToString());
            Assert.Equal(botClientId, host.HostClientId);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullName()
        {
            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty ?? null));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyName()
        {
            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnUnknownChannel()
        {
            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel("random"));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannel()
        {
            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);
            host.Channels.Add(_defaultChannel, null);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelTokenProvider()
        {
            var botAlias = "bot1";
            var botClientId = "123";
            var botEndpoint = "http://localhost/api/messages";
            var botTokenProvider = "";
            var defaultHostEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {"ChannelHost:Channels:0:Alias", botAlias},
                {"ChannelHost:Channels:0:ConnectionSettings:ClientId", botClientId},
                {"ChannelHost:Channels:0:ConnectionSettings:Endpoint", botEndpoint},
                {"ChannelHost:Channels:0:ConnectionSettings:TokenProvider", botTokenProvider},
                {"ChannelHost:DefaultHostEndpoint", defaultHostEndpoint},
                {"ChannelHost:HostClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            Assert.Throws<ArgumentException>(() => new ConfigurationChannelHost(config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullConnection()
        {
            var botAlias = "bot1";
            var botClientId = "123";
            var botEndpoint = "http://localhost/api/messages";
            var defaultHostEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {"ChannelHost:Channels:0:Alias", botAlias},
                {"ChannelHost:Channels:0:ConnectionSettings:ClientId", botClientId},
                {"ChannelHost:Channels:0:ConnectionSettings:Endpoint", botEndpoint},
                {"ChannelHost:DefaultHostEndpoint", defaultHostEndpoint},
                {"ChannelHost:HostClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object));
        }
    }
}

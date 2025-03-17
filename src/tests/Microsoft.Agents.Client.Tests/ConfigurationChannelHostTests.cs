// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class ConfigurationChannelHostTests
    {
        private readonly Mock<IKeyedServiceProvider> _provider = new();
        private readonly Mock<IConnections> _connections = new();
        private readonly IConfigurationRoot _config = new ConfigurationBuilder().Build();
        private readonly Mock<HttpBotChannelSettings> _channelInfo = new();
        private readonly Mock<IAccessTokenProvider> _token = new();
        private readonly Mock<IChannel> _channel = new();
        private readonly Mock<IConversationIdFactory> _conversationIdFactory = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
        private readonly IStorage _storage = new MemoryStorage();

        [Fact]
        public void Constructor_ShouldThrowOnEmptyConfigSection()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationChannelHost(_config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object, ""));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, null, _storage, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnections()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, _provider.Object, _storage, null, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldSetProperties()
        {
            var botName = "bot1";
            var botClientId = "123";
            var botTokenProvider = "BotServiceConnection";
            var botEndpoint = "http://localhost/api/messages";
            var defaultHostEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:ClientId", botClientId},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:TokenProvider", botTokenProvider},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:Endpoint", botEndpoint},
                {"ChannelHost:DefaultHostEndpoint", defaultHostEndpoint},
                {"ChannelHost:HostClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);

            Assert.Single(host._channels);
            Assert.Equal(botClientId, host._channels[botName].ConnectionSettings.ClientId);
            Assert.Equal(defaultHostEndpoint, host.DefaultHostEndpoint.ToString());
            Assert.Equal(botClientId, host.HostClientId);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyName()
        {
            var sections = new Dictionary<string, string>{
                {"ChannelHost:DefaultHostEndpoint", "http://localhost"},
                {"ChannelHost:HostClientId", "hostClientId"},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnUnknownChannel()
        {
            var botName = "botName";
            var botAlias = "bot1";
            var botClientId = "123";
            var botEndpoint = "http://localhost/api/messages";
            var botTokenProvider = "BotServiceConnection";
            var defaultHostEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {$"ChannelHost:Channels:{botName}:Alias", botAlias},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:ClientId", botClientId},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:Endpoint", botEndpoint},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:TokenProvider", botTokenProvider},
                {"ChannelHost:DefaultHostEndpoint", defaultHostEndpoint},
                {"ChannelHost:HostClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel("random"));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelTokenProvider()
        {
            var botName = "botName";
            var botAlias = "bot1";
            var botClientId = "123";
            var botEndpoint = "http://localhost/api/messages";
            var botTokenProvider = "";
            var defaultHostEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {$"ChannelHost:Channels:{botName}:Alias", botAlias},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:ClientId", botClientId},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:Endpoint", botEndpoint},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:TokenProvider", botTokenProvider},
                {"ChannelHost:DefaultHostEndpoint", defaultHostEndpoint},
                {"ChannelHost:HostClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            Assert.Throws<ArgumentException>(() => new ConfigurationChannelHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullConnection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, _provider.Object, _storage, null, _httpClientFactory.Object));
        }

        [Fact]
        public async Task Conversation_CreateDelete()
        {
            // arrange
            var botName = "botName";
            var botAlias = "bot1";
            var botClientId = "123";
            var botTokenProvider = "BotServiceConnection";
            var botEndpoint = "http://localhost/api/messages";
            var defaultHostEndpoint = "http://localhost/";
            var hostId = "hostId";
            var sections = new Dictionary<string, string>{
                {$"ChannelHost:Channels:{botName}:Alias", botAlias},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:ClientId", botClientId},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:TokenProvider", botTokenProvider},
                {$"ChannelHost:Channels:{botName}:ConnectionSettings:Endpoint", botEndpoint},
                {"ChannelHost:DefaultHostEndpoint", defaultHostEndpoint},
                {"ChannelHost:HostClientId", hostId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);


            // act
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Id = "1234",
                ChannelId = "webchat",
                Conversation = new ConversationAccount()
                {
                    Id = "1"
                },
                From = new ChannelAccount()
                {
                    Id = "me@from.com"
                }
            };
            var turnContext = new TurnContext(new TestAdapter(), activity)
            {
                Identity = new ClaimsIdentity(
                [
                    new(AuthenticationConstants.AudienceClaim, host.HostClientId),
                    new(AuthenticationConstants.AppIdClaim, host.HostClientId),
                ])
            };
            
            var turnState = new TurnState(_storage);
            await turnState.LoadStateAsync(turnContext);

            // should be no conversation for bot
            Assert.Null(host.GetExistingConversation(turnContext, turnState.Conversation, botName));

            // create a new conversation
            var conversationId = await host.GetOrCreateConversationAsync(turnContext, turnState.Conversation, botName);
            Assert.NotNull(conversationId);
            Assert.Equal(conversationId, host.GetExistingConversation(turnContext, turnState.Conversation, botName));

            // Verify ConversationIdFactory stored the reference
            var idState = await _storage.ReadAsync([conversationId], CancellationToken.None);
            Assert.Single(idState);

            // Verify ConversationState has the conversationId for the bot
            var conversations = turnState.GetValue<IDictionary<string, string>>("conversation.channelHost.channelConversations", () => new Dictionary<string, string>());
            Assert.Equal(conversationId, conversations[botName]);

            // delete conversation
            await host.DeleteConversationAsync(conversationId, turnState.Conversation);
            Assert.Null(host.GetExistingConversation(turnContext, turnState.Conversation, botName));

            // Verify ConversationIdFactory deleted the reference
            idState = await _storage.ReadAsync([conversationId], CancellationToken.None);
            Assert.Empty(idState);

            // Verify conversation for the bot was removed from ConversationState
            conversations = turnState.GetValue<IDictionary<string, string>>("conversation.channelHost.channelConversations");
            Assert.Empty(conversations);
        }

        [Fact]
        public async Task Conversation_MultiChannel()
        {
            // arrange
            var channel1Name = "bot1Name";
            var channel2Name = "bot2Name";
            var sections = new Dictionary<string, string>{
                {$"ChannelHost:Channels:{channel1Name}:Alias", channel1Name},
                {$"ChannelHost:Channels:{channel1Name}:ConnectionSettings:ClientId", "123"},
                {$"ChannelHost:Channels:{channel1Name}:ConnectionSettings:TokenProvider", "BotServiceConnection"},
                {$"ChannelHost:Channels:{channel1Name}:ConnectionSettings:Endpoint", "http://localhost/api/messages"},
                {$"ChannelHost:Channels:{channel2Name}:Alias", channel2Name},
                {$"ChannelHost:Channels:{channel2Name}:ConnectionSettings:ClientId", "456"},
                {$"ChannelHost:Channels:{channel2Name}:ConnectionSettings:TokenProvider", "BotServiceConnection"},
                {$"ChannelHost:Channels:{channel2Name}:ConnectionSettings:Endpoint", "http://localhost/api/messages"},
                {"ChannelHost:DefaultHostEndpoint", "http://localhost/"},
                {"ChannelHost:HostClientId", "hostId"},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);


            // act
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Id = "1234",
                ChannelId = "webchat",
                Conversation = new ConversationAccount()
                {
                    Id = "1"
                },
                From = new ChannelAccount()
                {
                    Id = "me@from.com"
                }
            };
            var turnContext = new TurnContext(new TestAdapter(), activity)
            {
                Identity = new ClaimsIdentity(
                [
                    new(AuthenticationConstants.AudienceClaim, host.HostClientId),
                    new(AuthenticationConstants.AppIdClaim, host.HostClientId),
                ])
            };

            var turnState = new TurnState(_storage);
            await turnState.LoadStateAsync(turnContext);

            // create a new conversation for channel1
            var conversationId1 = await host.GetOrCreateConversationAsync(turnContext, turnState.Conversation, channel1Name);
            Assert.NotNull(conversationId1);
            Assert.Equal(conversationId1, host.GetExistingConversation(turnContext, turnState.Conversation, channel1Name));

            // create a new conversation for channel2
            var conversationId2 = await host.GetOrCreateConversationAsync(turnContext, turnState.Conversation, channel2Name);
            Assert.NotNull(conversationId2);
            Assert.Equal(conversationId2, host.GetExistingConversation(turnContext, turnState.Conversation, channel2Name));

            // Should have two existing conversations
            var conversations = host.GetExistingConversations(turnContext, turnState.Conversation);
            Assert.Equal(2, conversations.Count);
            Assert.Equal(conversationId1, conversations.Where(c => c.ChannelName == channel1Name).First().ChannelConversationId);
            Assert.Equal(conversationId2, conversations.Where(c => c.ChannelName == channel2Name).First().ChannelConversationId);

            // delete conversation
            await host.DeleteConversationAsync(conversationId1, turnState.Conversation);
            await host.DeleteConversationAsync(conversationId2, turnState.Conversation);
            Assert.Null(host.GetExistingConversation(turnContext, turnState.Conversation, channel1Name));
            Assert.Null(host.GetExistingConversation(turnContext, turnState.Conversation, channel2Name));
        }
    }
}
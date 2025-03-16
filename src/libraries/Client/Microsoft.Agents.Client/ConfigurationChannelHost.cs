// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Client.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Loads bot host information from configuration.
    /// </summary>
    public class ConfigurationChannelHost : IChannelHost
    {
        private const string ChannelConversationsProperty = "conversation.channelHost.channelConversations";

        private readonly IServiceProvider _serviceProvider;
        private readonly IConversationIdFactory _conversationIdFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnections _connections;

        public ConfigurationChannelHost(
            IConfiguration configuration,
            IServiceProvider systemServiceProvider, 
            IConversationIdFactory conversationIdFactory, 
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            string configSection = "ChannelHost")
        {
            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _conversationIdFactory = conversationIdFactory ?? throw new ArgumentNullException(nameof(conversationIdFactory));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            ArgumentException.ThrowIfNullOrEmpty(configSection);

            var hostEndpoint = configuration?.GetValue<string>($"{configSection}:DefaultHostEndpoint");
            if (!string.IsNullOrWhiteSpace(hostEndpoint))
            {
                DefaultHostEndpoint = new Uri(hostEndpoint);
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelHostMissingProperty, null, nameof(DefaultHostEndpoint));
            }

            var hostClientId = configuration?.GetValue<string>($"{configSection}:HostClientId");
            if (!string.IsNullOrWhiteSpace(hostClientId))
            {
                HostClientId = hostClientId;
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelHostMissingProperty, null, nameof(HostClientId));
            }

            var section = configuration?.GetSection($"{configSection}:Channels");
            var channels = section?.Get<HttpBotChannelSettings[]>();
            if (channels != null)
            {
                foreach (var channel in channels)
                {
                    // If the ServiceUrl isn't supplied at the Channel:ConnectionSettings use
                    // the DefaultHostEndpoint.
                    if (string.IsNullOrEmpty(channel.ConnectionSettings.ServiceUrl))
                    {
                        channel.ConnectionSettings.ServiceUrl = DefaultHostEndpoint.ToString();
                    }

                    channel.ValidateChannelSettings();
                    Channels.Add(channel.Alias, channel);
                }
            }
        }

        /// <inheritdoc />
        public Uri DefaultHostEndpoint { get; }

        /// <inheritdoc />
        public string HostClientId { get; }

        /// <inheritdoc />
        internal IDictionary<string, HttpBotChannelSettings> Channels { get; } = new Dictionary<string, HttpBotChannelSettings>();

        /// <inheritdoc/>
        public IChannel GetChannel(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (!Channels.TryGetValue(name, out HttpBotChannelSettings channelInfo))
            {
                throw new InvalidOperationException($"IChannelInfo not found for '{name}'");
            }

            return GetChannel(channelInfo);
        }

        /// <inheritdoc/>
        private IChannel GetChannel(HttpBotChannelSettings channelSettings)
        {
            ArgumentNullException.ThrowIfNull(channelSettings);

            return CreateChannel(channelSettings);
        }

        /// <inheritdoc/>
        public string GetExistingConversation(string channelName, ITurnState turnState)
        {
            var conversations = turnState.GetValue<IDictionary<string, string>>(ChannelConversationsProperty, () => new Dictionary<string, string>());
            if (conversations.TryGetValue(channelName, out var conversationId)) 
            { 
                return conversationId;
            }
            return null;
        }

        /// <inheritdoc/>
        public async Task<string> GetOrCreateConversationAsync(string channelName, ITurnState turnState, ClaimsIdentity identity, IActivity activity, CancellationToken cancellationToken = default)
        {
            var conversations = turnState.GetValue<IDictionary<string, string>>(ChannelConversationsProperty, () => new Dictionary<string, string>());
            if (conversations.TryGetValue(channelName, out var conversationId)) { return conversationId; }

            var options = new ConversationIdFactoryOptions
            {
                FromBotOAuthScope = BotClaims.GetTokenScopes(identity)?.First(),
                FromBotId = HostClientId,
                Activity = activity,
                Channel = Channels[channelName]
            };

            var channelConversationId = await _conversationIdFactory.CreateConversationIdAsync(options, cancellationToken).ConfigureAwait(false);
            conversations.Add(channelName, channelConversationId);
            return channelConversationId;
        }

        /// <inheritdoc/>
        public async Task DeleteConversationAsync(string channelConversationId, ITurnState state, CancellationToken cancellationToken = default)
        {
            await _conversationIdFactory.DeleteConversationReferenceAsync(channelConversationId, cancellationToken).ConfigureAwait(false);

            var conversations = state.GetValue<IDictionary<string, string>>(ChannelConversationsProperty, () => new Dictionary<string, string>());
            var botAlias = conversations.Where(kv => kv.Value.Equals(channelConversationId)).Select((kv) => kv.Key).FirstOrDefault();
            if (botAlias != null)
            {
                conversations.Remove(botAlias);
            }
        }

        /// <inheritdoc/>
        public async Task SendToChannel(string channelName, string channelConversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            var targetChannelInfo = Channels[channelName];
            using var channel = GetChannel(channelName);

            var response = await channel.SendActivityAsync<InvokeResponse>(channelConversationId, activity, cancellationToken).ConfigureAwait(false);

            // Check response status
            if (!(response.Status >= 200 && response.Status <= 299))
            {
                throw new HttpRequestException($"Error invoking the bot id: \"{targetChannelInfo.Alias}\" at \"{targetChannelInfo.ConnectionSettings.Endpoint}\" (status is {response.Status}). \r\n {response.Body}");
            }
        }

        /// <inheritdoc/>
        public Task<BotConversationReference> GetBotConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken)
        {
            return _conversationIdFactory.GetBotConversationReferenceAsync(channelConversationId, cancellationToken);
        }

        private IChannel CreateChannel(HttpBotChannelSettings channelInfo)
        {
            HttpClient httpClient = _httpClientFactory.CreateClient(nameof(HttpBotChannel));

            var tokenProviderName = channelInfo.ConnectionSettings.TokenProvider;
            if (!_connections.TryGetConnection(tokenProviderName, out var tokenProvider))
            {
                throw new ArgumentException($"TokenProvider '{tokenProviderName}' not found for Channel '{channelInfo.Alias}'");
            }

            return new HttpBotChannel(channelInfo, httpClient, tokenProvider, (ILogger<HttpBotChannel>) _serviceProvider.GetService(typeof(ILogger<HttpBotChannel>)));
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Client.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Loads bot host information from configuration.
    /// </summary>
    public class ConfigurationChannelHost : IChannelHost
    {
        public const string ChannelConversationsProperty = "channelHost.channelConversations";

        private readonly IServiceProvider _serviceProvider;
        private readonly IConversationIdFactory _conversationIdFactory;
        private readonly IStorage _storage;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnections _connections;
        internal IDictionary<string, HttpBotChannelSettings> _channels;

        public ConfigurationChannelHost(
            IServiceProvider systemServiceProvider,
            IStorage storage,
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            IDictionary<string, HttpBotChannelSettings> channels,
            string hostEndpoint,
            string hostClientId)
        {
            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));

            _conversationIdFactory = new ConversationIdFactory(storage);

            if (!string.IsNullOrWhiteSpace(hostEndpoint))
            {
                DefaultHostEndpoint = new Uri(hostEndpoint);
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelHostMissingProperty, null, nameof(DefaultHostEndpoint));
            }

            if (!string.IsNullOrWhiteSpace(hostClientId))
            {
                HostClientId = hostClientId;
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelHostMissingProperty, null, nameof(HostClientId));
            }

            LoadChannels(channels);
        }

        /// <summary>
        /// Creates from IConfiguration.
        /// </summary>
        /// <code>
        /// "ChannelHost": {
        ///   "HostClientId": "{{ClientId}}",                                  // This is the Client ID used for the remote bot to call you back with.,
        ///   "DefaultHostEndpoint": "http://localhost:3978/api/botresponse/", // Default host serviceUrl.  Channel can override this via Channel:{{name}}:ConnectionSettings:ServiceUrl
        ///   "Channels": {
        ///      "EchoBot": {
        ///        "DisplayName": {{optional-displayName}},              // Defaults to node name ("EchoBot")
        ///        "ConnectionSettings": {
        ///          "ClientId": "{{Bot2ClientId}}",                     // This is the Client ID of the other agent.
        ///          "Endpoint": "http://localhost:39783/api/messages",  // The endpoint of the other agent
        ///          "TokenProvider" : "{{Connections:{{name}}"
        ///        }
        ///     }
        ///   }
        /// }
        /// </code>
        /// <param name="configuration"></param>
        /// <param name="systemServiceProvider"></param>
        /// <param name="storage"></param>
        /// <param name="connections"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="configSection"></param>
        public ConfigurationChannelHost(
            IConfiguration configuration,
            IServiceProvider systemServiceProvider,
            IStorage storage, 
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            string configSection = "ChannelHost") : this(
                systemServiceProvider, 
                storage, 
                connections, 
                httpClientFactory,
                configuration?.GetSection($"{configSection}:Channels").Get<IDictionary<string, HttpBotChannelSettings>>(), 
                configuration?.GetValue<string>($"{configSection}:DefaultHostEndpoint"), 
                configuration?.GetValue<string>($"{configSection}:HostClientId"))
        {
        }

        private void LoadChannels(IDictionary<string, HttpBotChannelSettings> channels)
        {
            _channels = channels ?? new Dictionary<string, HttpBotChannelSettings>();
            if (channels != null)
            {
                foreach (var kv in _channels)
                {
                    // If the ServiceUrl isn't supplied at the Channel:ConnectionSettings use
                    // the DefaultHostEndpoint.
                    if (string.IsNullOrEmpty(kv.Value.ConnectionSettings.ServiceUrl))
                    {
                        kv.Value.ConnectionSettings.ServiceUrl = DefaultHostEndpoint.ToString();
                    }

                    kv.Value.Name = kv.Key;

                    if (string.IsNullOrEmpty(kv.Value.DisplayName))
                    {
                        kv.Value.DisplayName = kv.Value.Name;
                    }

                    kv.Value.ValidateChannelSettings();
                }
            }
        }

        /// <inheritdoc />
        public Uri DefaultHostEndpoint { get; set; }

        /// <inheritdoc />
        public string HostClientId { get; set; }

        /// <inheritdoc/>
        public IChannel GetChannel(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (!_channels.TryGetValue(name, out HttpBotChannelSettings channelSettings))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelNotFound, null, name);
            }

            return CreateChannel(name, channelSettings);
        }

        /// <inheritdoc/>
        public string GetExistingConversation(ITurnContext turnContext, ConversationState conversationState, string channelName)
        {
            var conversations = conversationState.GetValue<IDictionary<string, string>>(ChannelConversationsProperty, () => new Dictionary<string, string>());
            if (conversations.TryGetValue(channelName, out var conversationId)) 
            { 
                return conversationId;
            }
            return null;
        }

        /// <inheritdoc/>
        public IList<ChannelConversation> GetExistingConversations(ITurnContext turnContext, ConversationState conversationState)
        {
            var result = new List<ChannelConversation>();
            var conversations = conversationState.GetValue<IDictionary<string, string>>(ChannelConversationsProperty, () => new Dictionary<string, string>());
            foreach (var conversation in conversations)
            {
                result.Add(new ChannelConversation() { ChannelName = conversation.Key, ChannelConversationId = conversation.Value });
            }
            return result;
        }

        /// <inheritdoc/>
        public async Task<string> GetOrCreateConversationAsync(ITurnContext turnContext, ConversationState conversationState, string channelName, CancellationToken cancellationToken = default)
        {
            //ClaimsIdentity identity, IActivity activity
            var conversations = conversationState.GetValue<IDictionary<string, string>>(ChannelConversationsProperty, () => new Dictionary<string, string>());
            if (conversations.TryGetValue(channelName, out var conversationId)) { return conversationId; }

            var options = new ConversationIdFactoryOptions
            {
                FromBotOAuthScope = BotClaims.GetTokenScopes(turnContext.Identity)?.First(),
                FromBotId = HostClientId,
                Activity = turnContext.Activity,
                Channel = _channels[channelName]
            };

            var channelConversationId = await _conversationIdFactory.CreateConversationIdAsync(options, cancellationToken).ConfigureAwait(false);
            conversations.Add(channelName, channelConversationId);
            return channelConversationId;
        }

        /// <inheritdoc/>
        public async Task DeleteConversationAsync(string channelConversationId, ConversationState conversationState, CancellationToken cancellationToken = default)
        {
            await _conversationIdFactory.DeleteConversationReferenceAsync(channelConversationId, cancellationToken).ConfigureAwait(false);

            var conversations = conversationState.GetValue<IDictionary<string, string>>(ChannelConversationsProperty, () => new Dictionary<string, string>());
            var botAlias = conversations.Where(kv => kv.Value.Equals(channelConversationId)).Select((kv) => kv.Key).FirstOrDefault();
            if (botAlias != null)
            {
                conversations.Remove(botAlias);
            }
        }

        /// <inheritdoc/>
        public async Task SendToChannel(string channelName, string channelConversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            using var channel = GetChannel(channelName);

            InvokeResponse<object> response = null;
            try
            {
                response = await channel.SendActivityAsync<object>(channelConversationId, activity, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.SendToChannelFailed, ex, channelName);
            }

            // Check response status
            if (!(response.Status >= 200 && response.Status <= 299))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.SendToChannelFailed, null, channelName, response.Status.ToString());
            }
        }

        /// <inheritdoc/>
        public Task<BotConversationReference> GetBotConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken = default)
        {
            return _conversationIdFactory.GetBotConversationReferenceAsync(channelConversationId, cancellationToken);
        }

        private IChannel CreateChannel(string name, HttpBotChannelSettings channelSettings)
        {
            var tokenProviderName = channelSettings.ConnectionSettings.TokenProvider;
            if (!_connections.TryGetConnection(tokenProviderName, out var tokenProvider))
            {
                throw new ArgumentException($"TokenProvider '{tokenProviderName}' not found for Channel '{name}'");
            }

            return new HttpBotChannel(channelSettings, _httpClientFactory, tokenProvider, (ILogger<HttpBotChannel>) _serviceProvider.GetService(typeof(ILogger<HttpBotChannel>)));
        }
    }
}

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
    /// Loads channel information from configuration.
    /// </summary>
    public class ConfigurationAgentHost : IAgentHost
    {
        public const string ChannelConversationsProperty = "agentHost.channelConversations";

        private readonly IServiceProvider _serviceProvider;
        private readonly IConversationIdFactory _conversationIdFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnections _connections;
        internal IDictionary<string, HttpAgentChannelSettings> _channels;

        public ConfigurationAgentHost(
            IServiceProvider systemServiceProvider,
            IStorage storage,
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            IDictionary<string, HttpAgentChannelSettings> channels,
            string hostEndpoint,
            string hostClientId)
        {
            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
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
        /// "Agent": {
        ///   "ClientId": "{{ClientId}}",                                  // This is the Client ID used for the remote agent to call you back with.,
        ///   "Endpoint": "http://myagent.com/api/messages",               // Optional
        ///   "Description": null,                                         // Optional
        ///   "Publisher": null,                                           // Optional
        ///   "Copyright": null,                                           // Optional
        ///   "Host": {
        ///     "DefaultEndpoint": "http://localhost:3978/api/channelresponse/", // Default host serviceUrl.  Channel can override this via Channel:{{name}}:ConnectionSettings:ServiceUrl
        ///     "Agents": {
        ///       "Echo": {
        ///         "DisplayName": {{optional-displayName}},               // Defaults to node name ("Echo")
        ///         "ConnectionSettings": {
        ///           "ClientId": "{{Agent2ClientId}}",                    // This is the Client ID of the other agent.
        ///           "Endpoint": "http://localhost:39783/api/messages",   // The endpoint of the other agent
        ///           "TokenProvider" : "{{Connections:{{name}}"
        ///         }
        ///       }
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
        public ConfigurationAgentHost(
            IConfiguration configuration,
            IServiceProvider systemServiceProvider,
            IStorage storage, 
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            string configSection = "Agent") : this(
                systemServiceProvider, 
                storage, 
                connections, 
                httpClientFactory,
                configuration?.GetSection($"{configSection}:Host:Agents").Get<IDictionary<string, HttpAgentChannelSettings>>(), 
                configuration?.GetValue<string>($"{configSection}:Host:DefaultEndpoint"), 
                configuration?.GetValue<string>($"{configSection}:ClientId"))
        {
        }

        private void LoadChannels(IDictionary<string, HttpAgentChannelSettings> channels)
        {
            _channels = channels ?? new Dictionary<string, HttpAgentChannelSettings>();
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
        public IAgentChannel GetChannel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelNotFound, null, "<null>");
            }

            if (!_channels.TryGetValue(name, out HttpAgentChannelSettings channelSettings))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelNotFound, null, name);
            }

            return CreateChannel(name, channelSettings);
        }

        /// <inheritdoc />
        public IList<IAgentChannelInfo> GetChannels()
        {
            var result = new List<IAgentChannelInfo>();

            foreach (var channel in _channels.Values)
            {
                result.Add(channel);
            }

            return result;
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
                FromOAuthScope = BotClaims.GetTokenScopes(turnContext.Identity)?.First(),
                FromClientId = HostClientId,
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
            var channelName = conversations.Where(kv => kv.Value.Equals(channelConversationId)).Select((kv) => kv.Key).FirstOrDefault();
            if (channelName != null)
            {
                conversations.Remove(channelName);
            }
        }

        /// <inheritdoc/>
        public async Task SendToAgent(string channelName, string channelConversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            using var channel = GetChannel(channelName);

            if (string.IsNullOrEmpty(activity.DeliveryMode)
                || !string.Equals(DeliveryModes.Normal, activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                activity.DeliveryMode = DeliveryModes.Normal;
            }

            await channel.SendActivityAsync<object>(channelConversationId, activity, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task EndAllActiveConversations(ITurnContext turnContext, ConversationState conversationState, CancellationToken cancellationToken = default)
        {
            // End all active channel conversations.
            var activeConversations = GetExistingConversations(turnContext, conversationState);
            if (activeConversations.Count > 0)
            {
                foreach (var conversation in activeConversations)
                {
                    // Delete the conversation because we're done with it.
                    await DeleteConversationAsync(conversation.ChannelConversationId, conversationState, cancellationToken).ConfigureAwait(false);

                    // Send EndOfConversation to the Agent.
                    await SendToAgent(conversation.ChannelName, conversation.ChannelConversationId, Activity.CreateEndOfConversationActivity(), cancellationToken).ConfigureAwait(false);
                }
            }

            await conversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<ChannelConversationReference> GetChannelConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken = default)
        {
            return _conversationIdFactory.GetChannelConversationReferenceAsync(channelConversationId, cancellationToken);
        }

        private IAgentChannel CreateChannel(string name, HttpAgentChannelSettings channelSettings)
        {
            var tokenProviderName = channelSettings.ConnectionSettings.TokenProvider;
            if (!_connections.TryGetConnection(tokenProviderName, out var tokenProvider))
            {
                throw new ArgumentException($"TokenProvider '{tokenProviderName}' not found for Channel '{name}'");
            }

            return new HttpAgentChannel(channelSettings, _httpClientFactory, tokenProvider, (ILogger<HttpAgentChannel>) _serviceProvider.GetService(typeof(ILogger<HttpAgentChannel>)));
        }
    }
}

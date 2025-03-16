// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Sends Activities to a remote Agent.
    /// </summary>
    internal class HttpBotChannel : IChannel
    {
        private readonly HttpBotChannelSettings _settings;
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private bool _disposed;

        /// <param name="channelSettings"></param>
        /// <param name="httpClient"></param>
        /// <param name="tokenProvider"></param>
        /// <param name="logger"></param>
        public HttpBotChannel(
            HttpBotChannelSettings channelSettings,
            HttpClient httpClient,
            IAccessTokenProvider tokenProvider,
            ILogger<HttpBotChannel> logger = null)
        {
            _settings = channelSettings ?? throw new ArgumentNullException(nameof(channelSettings));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? NullLogger<HttpBotChannel>.Instance;

            channelSettings.ValidateChannelSettings();
        }

        /// <inheritdoc/>
        public string Alias => _settings.Alias;

        /// <inheritdoc/>
        public string DisplayName => _settings.DisplayName;

        /// <inheritdoc/>
        public async Task SendActivityAsync(string channelConversationId, IActivity activity, CancellationToken cancellationToken, IActivity relatesTo = null)
        {
            await SendActivityAsync<object>(channelConversationId, activity, cancellationToken, relatesTo).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<InvokeResponse<T>> SendActivityAsync<T>(string channelConversationId, IActivity activity, CancellationToken cancellationToken, IActivity relatesTo = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelConversationId);
            ArgumentNullException.ThrowIfNull(activity);

            _logger.LogInformation($"SendActivityAsync: '{_settings.ConnectionSettings.ClientId}' at '{_settings.ConnectionSettings.Endpoint.ToString()}'");

            // Clone the activity so we can modify it before sending without impacting the original object.
            var activityClone = CreateSendActivity(channelConversationId, activity, relatesTo);

            // Create the HTTP request from the cloned Activity and send it to the bot.
            using var response = await SendRequest(channelConversationId, activityClone, cancellationToken).ConfigureAwait(false);
            var content = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : null;

            if (response.IsSuccessStatusCode)
            {
                // On success assuming either JSON that can be deserialized to T or empty.
                return new InvokeResponse<T>
                {
                    Status = (int)response.StatusCode,
                    Body = content?.Length > 0 ? ProtocolJsonSerializer.ToObject<T>(content) : default
                };
            }
            else
            {
                // Otherwise we can assume we don't have a T to deserialize - so just log the content so it's not lost.
                _logger.LogError($"SendActivityAsync: Bot request failed to '{_settings.ConnectionSettings.Endpoint.ToString()}' returning '{(int)response.StatusCode}' and '{content}'");

                // We want to at least propagate the status code because that is what InvokeResponse expects.
                return new InvokeResponse<T>
                {
                    Status = (int)response.StatusCode,
                    Body = typeof(T) == typeof(object) ? (T)(object)content : default,
                };
            }
        }

        private async Task<HttpResponseMessage> SendRequest(string channelConversationId, IActivity activity, CancellationToken cancellationToken)
        {
            var jsonContent = new StringContent(activity.ToJson(), Encoding.UTF8, "application/json");
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = _settings.ConnectionSettings.Endpoint,
                Content = jsonContent
            };

            httpRequestMessage.Headers.Add(ConversationConstants.ConversationIdHttpHeaderName, channelConversationId);

            // Add the auth header to the HTTP request.
            var tokenResult = await _tokenProvider.GetAccessTokenAsync(_settings.ConnectionSettings.ResourceUrl, [$"{_settings.ConnectionSettings.ClientId}/.default"]).ConfigureAwait(false);
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult);

            var completionOption = HttpCompletionOption.ResponseContentRead; // activity.DeliveryMode == DeliveryModes.Stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
            return await _httpClient.SendAsync(httpRequestMessage, completionOption, cancellationToken).ConfigureAwait(false);
        }

        private IActivity CreateSendActivity(string channelConversationId, IActivity activity, IActivity relatesTo)
        {
            // Clone the activity so we can modify it before sending without impacting the original object.
            var activityClone = activity.Clone();

            // Apply the appropriate addressing to the newly created Activity.
            if (relatesTo != null)
            {
                activityClone.RelatesTo = new ConversationReference
                {
                    ServiceUrl = relatesTo.ServiceUrl,
                    ActivityId = relatesTo.Id,
                    ChannelId = relatesTo.ChannelId,
                    Locale = relatesTo.Locale,
                    Conversation = new ConversationAccount
                    {
                        Id = relatesTo.Conversation.Id,
                        Name = relatesTo.Conversation.Name,
                        ConversationType = relatesTo.Conversation.ConversationType,
                        AadObjectId = relatesTo.Conversation.AadObjectId,
                        IsGroup = relatesTo.Conversation.IsGroup,
                        Properties = relatesTo.Conversation.Properties,
                        Role = relatesTo.Conversation.Role,
                        TenantId = relatesTo.Conversation.TenantId,
                    }
                };
            }

            activityClone.ServiceUrl = _settings.ConnectionSettings.ServiceUrl;
            activityClone.Recipient ??= new ChannelAccount();
            activityClone.Recipient.Role = RoleTypes.Skill;

            activityClone.Conversation ??= new ConversationAccount();
            if (!string.IsNullOrEmpty(activityClone.Conversation.Id))
            {
                activityClone.Conversation.Id = channelConversationId;
            }

            return activityClone;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of dispose pattern.
        /// </summary>
        /// <param name="disposing">Indicates where this method is called from.</param>
        protected void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

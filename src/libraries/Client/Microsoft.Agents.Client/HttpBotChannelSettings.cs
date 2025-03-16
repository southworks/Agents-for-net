// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Client.Errors;
using System;

namespace Microsoft.Agents.Client
{
    internal class HttpBotChannelSettings : ChannelSettings
    {
        public HttpBotChannelSettings() 
        {
        }

        public ConnectionSettings ConnectionSettings { get; set; } = new ConnectionSettings();


        public override void ValidateChannelSettings()
        {
            base.ValidateChannelSettings();

            if (string.IsNullOrWhiteSpace(ConnectionSettings.ClientId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelMissingProperty, null, $"Channel:{Alias}:ConnectionSettings:{nameof(ConnectionSettings.ClientId)}");
            }

            if (ConnectionSettings.Endpoint == null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelMissingProperty, null, $"Channel:{Alias}:ConnectionSettings:{nameof(ConnectionSettings.Endpoint)}");
            }

            if (string.IsNullOrWhiteSpace(ConnectionSettings.TokenProvider))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelMissingProperty, null, $"Channel:{Alias}:ConnectionSettings:{nameof(ConnectionSettings.TokenProvider)}");
            }

            if (string.IsNullOrWhiteSpace(ConnectionSettings.ServiceUrl))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ChannelMissingProperty, null, $"Channel:{Alias}:ConnectionSettings:{nameof(ConnectionSettings.ServiceUrl)}");
            }

            if (string.IsNullOrEmpty(ConnectionSettings.ResourceUrl))
            {
                ConnectionSettings.ResourceUrl = $"api://{ConnectionSettings.ClientId}";
            }
        }
    }

    internal class ConnectionSettings
    {
        /// <summary>
        /// Gets or sets clientId/appId of the channel.
        /// </summary>
        /// <value>
        /// ClientId/AppId of the channel.
        /// </value>
        public string ClientId { get; set; }

        public string ResourceUrl { get; set; }

        /// <summary>
        /// Gets or sets provider name for tokens.
        /// </summary>
        public string TokenProvider { get; set; }

        /// <summary>
        /// Gets or sets endpoint for the channel.
        /// </summary>
        /// <value>
        /// Uri for the channel.
        /// </value>
        public Uri Endpoint { get; set; }

        public string ServiceUrl { get; set; }
    }
}

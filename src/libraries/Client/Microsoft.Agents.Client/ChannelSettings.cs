// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Client
{
    internal class ChannelSettings : IChannelInfo
    {
        public ChannelSettings()
        {

        }

        /// <summary>
        /// Gets or sets Alias of the channel.
        /// </summary>
        /// <value>
        /// Alias of the channel.
        /// </value>
        public string Alias { get; set; }

        public string DisplayName { get; set; }

        public virtual void ValidateChannelSettings() 
        { 
            ArgumentException.ThrowIfNullOrWhiteSpace(Alias);
        }
    }
}

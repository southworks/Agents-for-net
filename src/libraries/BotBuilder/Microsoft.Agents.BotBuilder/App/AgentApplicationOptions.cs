﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using Microsoft.Agents.BotBuilder.App.AdaptiveCards;
using System.Collections.Generic;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.BotBuilder.App
{
    /// <summary>
    /// Options for the <see cref="AgentApplication"/> class.
    /// </summary>
    public class AgentApplicationOptions
    {
        public AgentApplicationOptions() { }

        public AgentApplicationOptions(IConfiguration configuration, IChannelAdapter channelAdapter, IStorage storage, ILoggerFactory loggerFactory = null, string configurationSection = "AgentApplicationOptions") 
        { 
            Adapter = channelAdapter;
            TurnStateFactory = () => new TurnState(storage);  // Null storage will just create a TurnState with TempState.
            LoggerFactory = loggerFactory;

            var section = configuration.GetSection(configurationSection);
            StartTypingTimer = section.GetValue<bool>(nameof(StartTypingTimer), false);
            RemoveRecipientMention = section.GetValue<bool>(nameof(RemoveRecipientMention), false);
            NormalizeMentions = section.GetValue<bool>(nameof(NormalizeMentions), false);
        }

        public IChannelAdapter? Adapter { get; set; }

        /// <summary>
        /// Optional. Options used to customize the processing of Adaptive Card requests.
        /// </summary>
        public AdaptiveCardsOptions? AdaptiveCards { get; set; }

        /// <summary>
        /// Optional. Factory used to create a custom turn state instance.
        /// </summary>
        public Func<ITurnState>? TurnStateFactory { get; set; }

        /// <summary>
        /// Optional. Array of input file download plugins to use.
        /// </summary>
        public IList<IInputFileDownloader>? FileDownloaders { get; set; }

        /// <summary>
        /// Optional. Logger factory that will be used in this application.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>
        /// Optional. If true, the bot will automatically remove mentions of the bot's name from incoming
        /// messages. Defaults to true.
        /// </summary>
        public bool RemoveRecipientMention { get; set; } = false;

        /// <summary>
        /// Optional. If true, the bot will automatically normalize mentions across channels.
        /// Defaults to true.
        /// </summary>
        public bool NormalizeMentions { get; set; } = false;

        /// <summary>
        /// Optional. If true, the bot will automatically start a typing timer when messages are received.
        /// This allows the bot to automatically indicate that it's received the message and is processing
        /// the request. Defaults to true.
        /// </summary>
        public bool StartTypingTimer { get; set; } = false;

        /// <summary>
        /// Optional. Options used to enable user authentication for the application.
        /// </summary>
        public UserAuthenticationOptions UserAuthentication { get; set; }
    }
}
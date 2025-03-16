// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Agents.Client
{
    public static class ClientServiceCollectionExtensions
    {
        /// <summary>
        /// Adds bot-to-bot functionality.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="httpBotClientName"></param>
        /// <param name="storage">Used for ConversationIdFactory.  If null, the registered IStorage will be used.</param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddChannelHost(this IHostApplicationBuilder builder, IStorage storage = null)
        {
            // Add conversation id factory.  
            // This is a memory only implementation, and for production would require persistence.
            if (storage != null)
            {
                builder.Services.AddSingleton<IConversationIdFactory>(sp => new ConversationIdFactory(storage));
            }
            else
            {
                builder.Services.AddSingleton<IConversationIdFactory, ConversationIdFactory>();
            }

            // Add bot callback handler.  This is AgentApplication specific.
            // This is the object that handles callback endpoints for bot responses.
            builder.Services.AddTransient<AdapterBotResponseHandler>();
            builder.Services.AddTransient<IChannelApiHandler, AdapterBotResponseHandler>();

            // Add the bots configuration class.  This loads client info and known bots.
            builder.Services.AddSingleton<IChannelHost, ConfigurationChannelHost>();

            return builder;
        }
    }
}

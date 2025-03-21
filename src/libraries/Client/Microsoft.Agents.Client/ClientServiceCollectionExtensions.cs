// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;

namespace Microsoft.Agents.Client
{
    public static class ClientServiceCollectionExtensions
    {
        public static IHostApplicationBuilder AddAgentHost(this IHostApplicationBuilder builder, IStorage storage = null)
        {
            return builder.AddAgentHost<AdapterChannelResponseHandler>(storage);
        }

        /// <summary>
        /// Adds multi-Agent functionality.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="storage">Used for IAgentHost.  If null, the registered IStorage will be used.</param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddAgentHost<THandler>(this IHostApplicationBuilder builder, IStorage storage = null)
            where THandler : class, IChannelApiHandler
        {
            // Add channel callback handler.  This is AgentApplication specific.
            // This handles HTTP request for Connector API calls from another Agent.
            builder.Services.AddTransient<IChannelApiHandler, THandler>();

            // Add IChannelHost implementation.
            builder.Services.AddSingleton<IAgentHost, ConfigurationAgentHost>(sp =>
            {
                return new ConfigurationAgentHost(
                    builder.Configuration,
                    sp,
                    storage ?? sp.GetService<IStorage>(),
                    sp.GetService<IConnections>(),
                    sp.GetService<IHttpClientFactory>());
            });

            return builder;
        }
    }
}

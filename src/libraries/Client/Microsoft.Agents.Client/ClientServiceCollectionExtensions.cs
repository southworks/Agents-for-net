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
        /// <summary>
        /// Adds agent-to-agent functionality.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="storage">Used for IChannelHost.  If null, the registered IStorage will be used.</param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddChannelHost(this IHostApplicationBuilder builder, IStorage storage = null)
        {
            // Add channel callback handler.  This is AgentApplication specific.
            // This handles HTTP request for Connector API calls from another Agent.
            builder.Services.AddTransient<IChannelApiHandler, AdapterChannelResponseHandler>();

            // Add IChannelHost implementation.
            builder.Services.AddSingleton<IChannelHost, ConfigurationChannelHost>(sp =>
            {
                return new ConfigurationChannelHost(
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

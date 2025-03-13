// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.Client;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
        public static IHostApplicationBuilder AddBot(this IHostApplicationBuilder builder, Func<IServiceProvider, IBot> implementationFactory, IStorage storage = null)
        {
            return AddBot<CloudAdapter>(builder, implementationFactory, storage);
        }

        public static IHostApplicationBuilder AddBot<TAdapter>(this IHostApplicationBuilder builder, Func<IServiceProvider, IBot> implementationFactory, IStorage storage = null)
            where TAdapter : CloudAdapter
        {
            AddCore<TAdapter>(builder, storage);

            builder.Services.AddTransient<IBot>(implementationFactory);

            return builder;
        }

        public static IHostApplicationBuilder AddBot<TBot>(this IHostApplicationBuilder builder, IStorage storage = null)
            where TBot : class, IBot
        {
            return AddBot<TBot, CloudAdapter>(builder, storage);
        }

        public static IHostApplicationBuilder AddBot<TBot, TAdapter>(this IHostApplicationBuilder builder, IStorage storage = null)
            where TBot : class, IBot
            where TAdapter : CloudAdapter
        {
            AddCore<TAdapter>(builder, storage);

            // Add the Bot,  this is the primary worker for the bot. 
            builder.Services.AddTransient<IBot, TBot>();

            return builder;
        }

        /// <summary>
        /// Adds bot-to-bot functionality.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="httpBotClientName"></param>
        /// <param name="storage">Used for ConversationIdFactory.  If null, the registered IStorage will be used.</param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddChannelHost(this IHostApplicationBuilder builder, string httpBotClientName = "HttpBotClient", IStorage storage = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(httpBotClientName);

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
            builder.Services.AddTransient<IChannelApiHandler>((sp) => sp.GetService<AdapterBotResponseHandler>());

            // Add the bots configuration class.  This loads client info and known bots.
            builder.Services.AddSingleton<IChannelHost, ConfigurationChannelHost>(
                (sp) =>
                new ConfigurationChannelHost(
                    sp.GetRequiredService<IServiceProvider>(),
                    sp.GetRequiredService<IConversationIdFactory>(),
                    sp.GetRequiredService<IConnections>(),
                    sp.GetRequiredService<IConfiguration>(),
                    defaultChannelName: httpBotClientName // this ensures that the default channel name is set to the http bot client name.
                                                          // Note: if this is overridden in the configuration, the value passed as httpBotClientName must also be updated.
                                                          //     It is not expected this will be needed for most customers. 
                    ));

            // Add bot client factory for HTTP
            // Use the same auth connection as the ChannelServiceFactory for now.
            builder.Services.AddKeyedSingleton<IChannelFactory>(httpBotClientName, (sp, key) => new HttpBotChannelFactory(
                sp.GetService<IHttpClientFactory>(),
                (ILogger<HttpBotChannelFactory>)sp.GetService(typeof(ILogger<HttpBotChannelFactory>))));

            return builder;
        }

        /// <summary>
        /// Add the default CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="async"></param>
        public static void AddCloudAdapter(this IServiceCollection services)
        {
            services.AddCloudAdapter<CloudAdapter>();
        }

        /// <summary>
        /// Add the derived CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="async"></param>
        public static void AddCloudAdapter<T>(this IServiceCollection services) where T : CloudAdapter
        {
            AddAsyncCloudAdapterSupport(services);

            services.AddSingleton<CloudAdapter, T>();
            services.AddSingleton<IBotHttpAdapter>(sp => sp.GetService<CloudAdapter>());
            services.AddSingleton<IChannelAdapter>(sp => sp.GetService<CloudAdapter>());
        }

        private static void AddCore<TAdapter>(this IHostApplicationBuilder builder, IStorage storage = null)
            where TAdapter : CloudAdapter
        {
            // Add Connections object to access configured token connections.
            builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

            // Add factory for ConnectorClient and UserTokenClient creation
            builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

            // Add IStorage for turn state persistence
            if (storage != null)
            {
                builder.Services.AddSingleton(storage);
            }
            else
            {
                var diStorage = builder.Services.Where(s => s.ServiceType == typeof(IStorage)).Any();
                if (!diStorage)
                {
                    builder.Services.AddSingleton<IStorage, MemoryStorage>();
                }
            }

            // Add the ChannelAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol.
            AddCloudAdapter<TAdapter>(builder.Services);
        }

        private static void AddAsyncCloudAdapterSupport(this IServiceCollection services)
        {
            // Activity specific BackgroundService for processing authenticated activities.
            services.AddHostedService<HostedActivityService>();
            // Generic BackgroundService for processing tasks.
            services.AddHostedService<HostedTaskService>();

            // BackgroundTaskQueue and ActivityTaskQueue are the entry points for
            // the enqueueing activities or tasks to be processed by the BackgroundService.
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddSingleton<IActivityTaskQueue, ActivityTaskQueue>();
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds an Agent which subclasses <c>AgentApplication</c>
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgentApplicationOptions();
        /// builder.AddAgent&lt;MyAgent&gt;();
        /// </code>
        /// </summary>
        /// <remarks>
        /// This will also call <see cref="AddAgentCore(IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient.
        /// </remarks>
        /// <typeparam name="TAgent"></typeparam>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgent<TAgent>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
        {
            return AddAgent<TAgent, CloudAdapter>(builder);
        }

        /// <summary>
        /// Same as <see cref="AddAgent{TAgent}(IHostApplicationBuilder)"/> but allows for use of
        /// any <c>CloudAdapter</c> subclass.
        /// </summary>
        /// <typeparam name="TAgent"></typeparam>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgent<TAgent, TAdapter>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
            where TAdapter : CloudAdapter
        {
            AddAgentCore<TAdapter>(builder);

            // Add the IAgent 
            if (!builder.Services.Any(x => x.ServiceType == typeof(IAgent)))
            {
                // There can only be one IAgent.
                builder.Services.AddTransient<IAgent, TAgent>();
            }

            // Add the TAgent (required for multi agent registrations)
            if (!builder.Services.Any(x => x.ServiceType == typeof(TAgent)))
            {
                // There can only be one TAgent.
                builder.Services.AddTransient<TAgent>();
            }

            return builder;
        }

        /// <summary>
        /// Adds an Agent via lambda construction.
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgentApplicationOptions();
        /// builder.AddAgent(sp =>
        /// {
        ///    var options = new AgentApplicationOptions()
        ///    {
        ///       TurnStateFactory = () => new TurnState(sp.GetService&lt;IStorage&gt;());
        ///    };
        ///        
        ///    var app = new AgentApplication(options);
        ///
        ///    ...
        ///
        ///    return app;
        /// });
        /// </code>
        /// </summary>
        /// <remarks>
        /// This will also calls <see cref="AddAgentCore(IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        public static IHostApplicationBuilder AddAgent(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
        {
            return AddAgent<CloudAdapter>(builder, implementationFactory);
        }

        /// <summary>
        /// This is the same as <see cref="AddAgent(IHostApplicationBuilder, Func{IServiceProvider, IAgent})"/>, except allows the
        /// use of any <c>CloudAdapter</c> subclass.
        /// </summary>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        public static IHostApplicationBuilder AddAgent<TAdapter>(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
            where TAdapter : CloudAdapter
        {
            AddAgentCore<TAdapter>(builder);

            builder.Services.AddTransient<IAgent>(implementationFactory);

            return builder;
        }

        /// <summary>
        /// Registers AgentApplicationOptions for AgentApplication-based Agents.
        /// </summary>
        /// <remarks>
        /// This loads options from IConfiguration and DI.  The <c>AgentApplicationOptions</c> is
        /// added as a singleton.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="fileDownloaders"></param>
        /// <param name="autoSignIn"></param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddAgentApplicationOptions(
            this IHostApplicationBuilder builder,
            AutoSignInSelector autoSignIn = null)
        {
            if (autoSignIn != null)
            {
                builder.Services.AddSingleton<AutoSignInSelector>(sp => autoSignIn);
            }

            builder.Services.AddSingleton<AgentApplicationOptions>();

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
        /// Add a derived CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="async"></param>
        public static void AddCloudAdapter<T>(this IServiceCollection services) where T : CloudAdapter
        {
            AddAsyncAdapterSupport(services);

            if (!services.Any(x => x.ServiceType == typeof(T)))
            {
                services.AddSingleton<CloudAdapter, T>();
                services.AddSingleton<IAgentHttpAdapter>(sp => sp.GetService<CloudAdapter>());
                services.AddSingleton<IChannelAdapter>(sp => sp.GetService<CloudAdapter>());
            }
        }

        /// <summary>
        /// Adds a middleware that collects headers to be propagated.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseHeaderPropagation(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseMiddleware<HeaderPropagationMiddleware>();
        }

        /// <summary>
        /// Adds the core agent services.
        /// <list type="bullet">
        /// <item><c>IConnections, which uses IConfiguration for settings.</c></item>
        /// <item><c>IChannelServiceClientFactory</c> for ConnectorClient and UserTokenClient creations.  Needed for Azure Bot Service and Agent-to-Agent.</item>
        /// <item><c>CloudAdapter</c>, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.</item>
        /// </list>
        /// </summary>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgentCore(this IHostApplicationBuilder builder)
        {
            return builder.AddAgentCore<CloudAdapter>();
        }

        public static IHostApplicationBuilder AddAgentCore<TAdapter>(this IHostApplicationBuilder builder)
            where TAdapter : CloudAdapter
        {
            if (!builder.Services.Any(x => x.ServiceType == typeof(IConnections)))
            {
                // Add Connections object to access configured token connections.
                builder.Services.AddSingleton<IConnections, ConfigurationConnections>();
            }

            if (!builder.Services.Any(x => x.ServiceType == typeof(IChannelServiceClientFactory)))
            {
                // Add factory for ConnectorClient and UserTokenClient creation
                builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();
            }

            // Add the CloudAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.
            AddCloudAdapter<TAdapter>(builder.Services);

            return builder;
        }

        public static void AddAsyncAdapterSupport(this IServiceCollection services)
        {
            if (!services.Any(x => x.ServiceType == typeof(IActivityTaskQueue)))
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

        /// <summary>
        /// The delegate signature for custom AgentApplication request processing methods when specified in AgentInterfaceAttribute.ProcessDelegate.
        /// </summary>
        /// <param name="request">The HTTP request object, typically in a POST handler by a Controller.</param>
        /// <param name="response">The HTTP response object.</param>
        /// <param name="adapter">The IAgentHttpAdapter for this request.</param>
        /// <param name="agent">The bot implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public delegate Task ProcessRequestDelegate(HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken);
        public delegate Task ProcessRequestDelegate<TAdapter, TAgent>(HttpRequest request, HttpResponse response, TAdapter adapter, TAgent agent, CancellationToken cancellationToken)
            where TAgent : IAgent
            where TAdapter : IAgentHttpAdapter;

        /// <summary>
        /// This adds HTTP endpoints for all AgentApplications defined in the calling assembly.  Each AgentApplication must have been added using <see cref="AddAgent{TAgent}(IHostApplicationBuilder)"/>."/>
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="requireAuth"></param>
        /// <param name="defaultPath"></param>
        /// <exception cref="InvalidOperationException"/>
        public static IEndpointConventionBuilder MapAgentApplicationEndpoints(
            this IEndpointRouteBuilder endpoints, 
            bool requireAuth = true,
            [StringSyntax("Route")] string defaultPath = "/api/messages")
        {
            if (string.IsNullOrEmpty(defaultPath))
            {
                defaultPath = "/api/messages";
            }

            var agentGroup = endpoints.MapGroup("");
            if (requireAuth)
            {
                agentGroup.RequireAuthorization();
            }
            else
            {
                agentGroup.AllowAnonymous();
            }

            var allAgents = Assembly.GetCallingAssembly().GetTypes().Where(t => t.IsOrDerives(typeof(AgentApplication))).ToList();
            if (allAgents.Count == 0)
            {
                // This is to handle declaring an AgentApplication in an AddTransient lambda.
                var inlineAgent = endpoints.ServiceProvider.GetService<IAgent>() 
                    ?? throw new InvalidOperationException("No AgentApplications were found in the calling assembly. Ensure that at least one AgentApplication is defined.");
                allAgents.Add(inlineAgent.GetType());
            }

            foreach (var agent in allAgents)
            {
                var interfaces = agent.GetCustomAttributes<AgentInterfaceAttribute>(true)?.ToList();
                if (interfaces?.Count == 0)
                {
                    if (allAgents.Count == 1)
                    {
                        // If there is only one AgentApplication, we can default
                        interfaces = new List<AgentInterfaceAttribute>()
                        {
                            new(AgentTransportProtocol.ActivityProtocol, defaultPath)
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"No AgentInterfaceAttribute was found on Agent '{agent.FullName}'. When multiple AgentApplications are defined, each must have at least one AgentInterfaceAttribute.");
                    }
                }

                foreach (var agentInterface in interfaces)
                {
                    if (agentInterface.Protocol != AgentTransportProtocol.ActivityProtocol)
                    {
                        // Currently only ActivityProtocol is supported here.
                        continue;
                    }

                    agentGroup.MapMethods(agentInterface.Path, ["POST"],
                        async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IServiceProvider services, CancellationToken cancellationToken) =>
                        {
                            IAgent agentInstance = null;
                            try
                            {
                                agentInstance = (IAgent)services.GetRequiredService(agent);
                            }
                            catch (Exception)
                            {
                                // This is to handle declaring an AgentApplication in an AddTransient lambda.
                                agentInstance = (IAgent)services.GetRequiredService(typeof(IAgent));
                            }

                            if (!string.IsNullOrEmpty(agentInterface.ProcessDelegate))
                            {
                                var processMethod = agentInstance.GetType().GetMethod(agentInterface.ProcessDelegate, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) 
                                    ?? throw new InvalidOperationException($"The specified ProcessDelegate '{agentInterface.ProcessDelegate}' was not found on AgentApplication '{agentInstance.GetType().FullName}'.");

#if !NETSTANDARD
                                var processDelegate = processMethod.CreateDelegate<ProcessRequestDelegate>(agentInstance);
#else
                                var processDelegate = (ProcessRequestDelegate)processMethod.CreateDelegate(typeof(ProcessRequestDelegate), agentInstance);
#endif

                                await processDelegate(request, response, adapter, agentInstance, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                await adapter.ProcessAsync(request, response, agentInstance, cancellationToken).ConfigureAwait(false);
                            }
                        });
                }
            }

            return agentGroup;
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoint(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<IAgentHttpAdapter, IAgent> process = null)
        {
            return app.MapAgentEndpoint<IAgentHttpAdapter, IAgent>(requireAuth, path, process);
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoint<TAgent>(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<IAgentHttpAdapter, TAgent> process = null)
            where TAgent : IAgent
        {
            return app.MapAgentEndpoint<IAgentHttpAdapter, TAgent>(requireAuth, path, process);
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoint<TAdapter, TAgent>(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<TAdapter, TAgent> process = null)
            where TAgent : IAgent
            where TAdapter : IAgentHttpAdapter
        {
            var agentGroup = app.MapGroup("");
            if (requireAuth)
            {
                agentGroup.RequireAuthorization();
            }
            else
            {
                agentGroup.AllowAnonymous();
            }

            // This receives incoming messages from Azure Bot Service or other SDK Agents
            agentGroup.MapPost(path, async (HttpRequest request, HttpResponse response, TAdapter adapter, TAgent agent, CancellationToken cancellationToken) =>
            {
                if (process != null)
                {
                    await process(request, response, adapter, agent, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await adapter.ProcessAsync(request, response, agent, cancellationToken).ConfigureAwait(false);
                }
            });

            return agentGroup;
        }

        public static void MapAgentDefaultRootEndpoint(this WebApplication app)
        {
            var assemblyName = System.Reflection.Assembly.GetCallingAssembly().GetName().Name;
            var assemblyVersion = System.Reflection.Assembly.GetCallingAssembly().GetName().Version?.ToString() ?? "unknown";
            app.MapGet("/", () => $"Microsoft Agents SDK: {assemblyName}, version {assemblyVersion}");
        }

        private static bool IsOrDerives(this Type type, Type baseType)
        {
            if (type.Equals(baseType))
            {
                return true;
            }

            var current = type.BaseType;
            while (current != null)
            {
                if (current.Equals(baseType))
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }
    }
}
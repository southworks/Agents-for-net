// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Extension methods for registering the named pipe transport
    /// with the Agents SDK host builder.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds named pipe transport for the agent, enabling pipe-based
        /// communication with DirectLineFlex without any HTTP roundtrips.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <param name="pipeName">
        /// The named pipe base name (default: "bfv4.pipes").
        /// Two pipes are created: {pipeName}.incoming and {pipeName}.outgoing.
        /// </param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentNamedPipeTransport(
            this IHostApplicationBuilder builder,
            string pipeName = "bfv4.pipes")
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Configuration[$"NamedPipe:PipeName"] = pipeName;

            builder.Services.AddHttpClient();

            builder.Services.AddSingleton<NamedPipeActivityHandler>();
            builder.Services.AddSingleton<NamedPipeMessageHandler>();
            builder.Services.AddHostedService<NamedPipeHostedService>();

            builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                {
                    var pipeHandler = handlerBuilder.Services.GetRequiredService<NamedPipeMessageHandler>();
                    handlerBuilder.AdditionalHandlers.Add(new PipeRoutingDelegatingHandler(pipeHandler));
                });
            });

            return builder;
        }
    }

    /// <summary>
    /// <see cref="DelegatingHandler"/> that routes urn:botframework:namedpipe requests to the
    /// <see cref="NamedPipeMessageHandler"/>, while passing all other requests through normally.
    /// </summary>
    internal sealed class PipeRoutingDelegatingHandler : DelegatingHandler
    {
        private readonly NamedPipeMessageHandler _pipeHandler;

        public PipeRoutingDelegatingHandler(NamedPipeMessageHandler pipeHandler)
        {
            _pipeHandler = pipeHandler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            if (uri != null && (
                uri.Scheme.Equals("urn", StringComparison.OrdinalIgnoreCase) ||
                uri.AbsoluteUri.Contains("botframework:namedpipe", StringComparison.OrdinalIgnoreCase)))
            {
                return _pipeHandler.SendViaPipeAsync(request, cancellationToken);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}

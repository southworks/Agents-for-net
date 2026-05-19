// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Handles inbound named pipe requests by invoking the agent's turn pipeline directly,
    /// without any HTTP roundtrip. Activities arrive from the named pipe and are passed
    /// to <see cref="IChannelAdapter.ProcessActivityAsync"/>.
    /// </summary>
    internal sealed class NamedPipeActivityHandler
    {
        private readonly IServiceProvider _services;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipeActivityHandler"/> class.
        /// </summary>
        /// <param name="services">The service provider for resolving scoped dependencies.</param>
        /// <param name="logger">The logger instance.</param>
        public NamedPipeActivityHandler(IServiceProvider services, ILogger<NamedPipeActivityHandler> logger)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Process an inbound named pipe request. Returns a <see cref="NamedPipeResponse"/>.
        /// </summary>
        /// <param name="request">The inbound request.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A response indicating the result of processing.</returns>
        public async Task<NamedPipeResponse> HandleAsync(NamedPipeRequest request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.Verb, "POST", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("NamedPipeActivityHandler: Unsupported verb {Verb} {Path}.", request.Verb, request.Path);
                return NamedPipeResponse.NotFound();
            }

            if (!string.Equals(request.Path, "/api/messages", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("NamedPipeActivityHandler: Unknown path {Path}, returning 404.", request.Path);
                return NamedPipeResponse.NotFound();
            }

            if (request.Body == null || request.Body.Length == 0)
            {
                _logger.LogWarning("NamedPipeActivityHandler: Empty body for POST /api/messages.");
                return new NamedPipeResponse { StatusCode = 400 };
            }

            _logger.LogDebug("NamedPipeActivityHandler: Processing activity (BodyLen={Len}).", request.Body.Length);

            try
            {
                var activity = JsonSerializer.Deserialize<Activity>(
                    request.Body, ProtocolJsonSerializer.SerializationOptions);

                if (activity == null)
                {
                    _logger.LogWarning("NamedPipeActivityHandler: Failed to deserialize activity.");
                    return new NamedPipeResponse { StatusCode = 400 };
                }

                using var scope = _services.CreateScope();
                var adapter = scope.ServiceProvider.GetRequiredService<IChannelAdapter>();
                var agent = scope.ServiceProvider.GetRequiredService<IAgent>();

                // DirectLine handles external auth before forwarding over the trusted pipe.
                // Keep this anonymous so the SDK does not require configured token connections
                // for connector clients whose outbound calls are routed back through the pipe.
                var identity = new ClaimsIdentity();

                _logger.LogInformation("NamedPipeActivityHandler: Invoking agent for activity type={Type} id={Id}.",
                    activity.Type, activity.Id);

                await adapter.ProcessActivityAsync(identity, activity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("NamedPipeActivityHandler: Agent completed successfully.");
                return NamedPipeResponse.Accepted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NamedPipeActivityHandler: Error processing activity.");
                return NamedPipeResponse.InternalServerError();
            }
        }
    }
}

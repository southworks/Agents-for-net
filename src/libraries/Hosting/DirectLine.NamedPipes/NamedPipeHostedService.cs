// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Background service that manages the named pipe server lifecycle.
    /// Accepts client connections, wires up the protocol, and handles reconnection.
    /// </summary>
    internal sealed class NamedPipeHostedService : BackgroundService
    {
        private readonly NamedPipeActivityHandler _activityHandler;
        private readonly NamedPipeMessageHandler _messageHandler;
        private readonly ILogger<NamedPipeHostedService> _logger;
        private readonly string _pipeName;

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipeHostedService"/> class.
        /// </summary>
        /// <param name="activityHandler">The activity handler for processing inbound requests.</param>
        /// <param name="messageHandler">The message handler for outbound pipe routing.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="configuration">The application configuration.</param>
        public NamedPipeHostedService(
            NamedPipeActivityHandler activityHandler,
            NamedPipeMessageHandler messageHandler,
            ILogger<NamedPipeHostedService> logger,
            IConfiguration configuration)
        {
            _activityHandler = activityHandler ?? throw new ArgumentNullException(nameof(activityHandler));
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipeName = configuration?.GetValue("NamedPipe:PipeName", "bfv4.pipes") ?? "bfv4.pipes";
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NamedPipeHostedService: Starting with pipe name '{PipeName}'.", _pipeName);

            while (!stoppingToken.IsCancellationRequested)
            {
                NamedPipeConnection connection = null;
                NamedPipeProtocol protocol = null;

                try
                {
                    connection = new NamedPipeConnection(_pipeName, _logger);
                    await connection.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);

                    protocol = new NamedPipeProtocol(connection.Reader, connection.Writer, _logger);
                    protocol.OnRequestReceived = _activityHandler.HandleAsync;

                    _messageHandler.SetProtocol(protocol);

                    protocol.Start();
                    _logger.LogInformation("NamedPipeHostedService: Protocol active on '{PipeName}'.", _pipeName);

                    while (!stoppingToken.IsCancellationRequested && connection.IsConnected)
                    {
                        await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
                    }

                    _logger.LogInformation("NamedPipeHostedService: Connection lost, will reconnect.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("NamedPipeHostedService: Shutting down.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NamedPipeHostedService: Error, retrying in 2s.");
                    await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    _messageHandler.SetProtocol(null);
                    if (protocol != null)
                    {
                        await protocol.DisposeAsync().ConfigureAwait(false);
                    }

                    if (connection != null)
                    {
                        await connection.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }
}

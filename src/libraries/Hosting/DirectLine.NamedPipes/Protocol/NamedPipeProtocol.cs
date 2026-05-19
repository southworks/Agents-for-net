// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol
{
    /// <summary>
    /// The named pipe protocol engine. Reads/writes framed messages over the transport,
    /// correlates request/response pairs, and dispatches to a handler.
    /// </summary>
    internal sealed class NamedPipeProtocol : IAsyncDisposable
    {
        /// <summary>
        /// Maximum allowed payload size per frame (4 MB). Prevents memory exhaustion
        /// from malformed or malicious headers.
        /// </summary>
        private const int MaxPayloadSize = 4 * 1024 * 1024;

        /// <summary>
        /// Maximum number of concurrent stream buffers. Limits memory usage from
        /// unmatched stream frames.
        /// </summary>
        private const int MaxStreamBuffers = 100;

        private readonly NamedPipeTransport _reader;
        private readonly NamedPipeTransport _writer;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly Dictionary<Guid, TaskCompletionSource<ReceiveResponse>> _pendingRequests = new();
        private CancellationTokenSource _cts;
        private Task _readLoop;

        /// <summary>
        /// Gets or sets the handler invoked when an inbound request is received
        /// (e.g., POST /api/messages from DirectLineFlex).
        /// </summary>
        public Func<NamedPipeRequest, CancellationToken, Task<NamedPipeResponse>> OnRequestReceived { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipeProtocol"/> class.
        /// </summary>
        /// <param name="reader">The transport for reading incoming frames.</param>
        /// <param name="writer">The transport for writing outgoing frames.</param>
        /// <param name="logger">The logger instance.</param>
        public NamedPipeProtocol(NamedPipeTransport reader, NamedPipeTransport writer, ILogger logger)
        {
            _reader = reader;
            _writer = writer;
            _logger = logger;
        }

        /// <summary>
        /// Start the background read loop that processes incoming frames.
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _readLoop = ReadLoopAsync(_cts.Token);
        }

        /// <summary>
        /// Send a request to the remote end and wait for its response.
        /// Used for outbound activities (agent → DirectLineFlex).
        /// </summary>
        /// <param name="verb">The HTTP verb.</param>
        /// <param name="path">The request path.</param>
        /// <param name="body">The request body, or null.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The response from the remote end.</returns>
        public async Task<ReceiveResponse> SendRequestAsync(string verb, string path, byte[] body, CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid();
            var tcs = new TaskCompletionSource<ReceiveResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingRequests)
            {
                _pendingRequests[requestId] = tcs;
            }

            try
            {
                var streamId = Guid.NewGuid();
                var requestPayload = new RequestPayload
                {
                    Verb = verb,
                    Path = path,
                    Streams = body != null
                        ? new List<PayloadDescription> { new PayloadDescription { Id = streamId.ToString("D"), ContentType = "application/json", Length = body.Length } }
                        : null
                };

                var payloadJson = JsonSerializer.SerializeToUtf8Bytes(requestPayload);

                await SendFrameAsync(PayloadTypes.Request, requestId, payloadJson, end: body == null, cancellationToken).ConfigureAwait(false);

                if (body != null)
                {
                    await SendFrameAsync(PayloadTypes.Stream, streamId, body, end: true, cancellationToken).ConfigureAwait(false);
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
                timeoutCts.Token.Register(() => tcs.TrySetCanceled());

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                lock (_pendingRequests)
                {
                    _pendingRequests.Remove(requestId);
                }
            }
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            var headerBuffer = new byte[HeaderSerializer.HeaderSize];
            var streamBuffers = new Dictionary<Guid, MemoryStream>();
            var pendingDispatches = new Dictionary<Guid, (Header header, RequestPayload payload)>();

            try
            {
                var connected = true;
                while (!cancellationToken.IsCancellationRequested && _reader.IsConnected && connected)
                {
                    if (!await _reader.ReadExactAsync(headerBuffer, HeaderSerializer.HeaderSize, cancellationToken).ConfigureAwait(false))
                    {
                        _logger.LogInformation("NamedPipeProtocol: Pipe disconnected (read returned 0).");
                        break;
                    }

                    var header = HeaderSerializer.Deserialize(headerBuffer);
                    _logger.LogDebug("NamedPipeProtocol: Frame type={Type} id={Id} len={Len} end={End}",
                        header.Type, header.Id, header.PayloadLength, header.End);

                    byte[] payload = null;
                    if (header.PayloadLength > 0)
                    {
                        if (header.PayloadLength > MaxPayloadSize)
                        {
                            _logger.LogError("NamedPipeProtocol: Payload size {Size} exceeds maximum {Max}. Disconnecting.",
                                header.PayloadLength, MaxPayloadSize);
                            break;
                        }

                        payload = new byte[header.PayloadLength];
                        if (!await _reader.ReadExactAsync(payload, header.PayloadLength, cancellationToken).ConfigureAwait(false))
                        {
                            _logger.LogWarning("NamedPipeProtocol: Pipe disconnected mid-payload.");
                            break;
                        }
                    }

                    switch (header.Type)
                    {
                        case PayloadTypes.Request:
                            HandleRequestFrame(header, payload, streamBuffers, pendingDispatches, cancellationToken);
                            break;

                        case PayloadTypes.Response:
                            HandleResponseFrame(header, payload, streamBuffers);
                            break;

                        case PayloadTypes.Stream:
                            connected = HandleStreamFrame(header, payload, streamBuffers, _logger);
                            break;

                        case PayloadTypes.CancelAll:
                            _logger.LogWarning("NamedPipeProtocol: Received CancelAll.");
                            break;

                        default:
                            _logger.LogWarning("NamedPipeProtocol: Unknown frame type '{Type}'.", header.Type);
                            break;
                    }

                    if (connected)
                    {
                        await TryDispatchPendingRequestsAsync(streamBuffers, pendingDispatches, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NamedPipeProtocol: Read loop error.");
            }
            finally
            {
                // Dispose any orphaned stream buffers to prevent memory leaks
                foreach (var ms in streamBuffers.Values)
                {
                    ms.Dispose();
                }

                streamBuffers.Clear();
                pendingDispatches.Clear();
            }
        }

        private void HandleRequestFrame(Header header, byte[] payload,
            Dictionary<Guid, MemoryStream> streamBuffers,
            Dictionary<Guid, (Header header, RequestPayload payload)> pendingDispatches,
            CancellationToken cancellationToken)
        {
            if (payload == null)
            {
                return;
            }

            var requestPayload = JsonSerializer.Deserialize<RequestPayload>(payload);
            if (requestPayload == null)
            {
                return;
            }

            if (requestPayload.Streams is { Count: > 0 })
            {
                var streamId = Guid.Parse(requestPayload.Streams[0].Id);
                if (!streamBuffers.ContainsKey(streamId))
                {
                    pendingDispatches[header.Id] = (header, requestPayload);
                    return;
                }
            }

            _ = DispatchRequestSafeAsync(header, requestPayload, streamBuffers, cancellationToken);
        }

        private Task TryDispatchPendingRequestsAsync(
            Dictionary<Guid, MemoryStream> streamBuffers,
            Dictionary<Guid, (Header header, RequestPayload payload)> pendingDispatches,
            CancellationToken cancellationToken)
        {
            var dispatched = new List<Guid>();
            foreach (var (requestId, pending) in pendingDispatches)
            {
                if (pending.payload.Streams is { Count: > 0 })
                {
                    var streamId = Guid.Parse(pending.payload.Streams[0].Id);
                    if (streamBuffers.ContainsKey(streamId))
                    {
                        dispatched.Add(requestId);
                        _ = DispatchRequestSafeAsync(pending.header, pending.payload, streamBuffers, cancellationToken);
                    }
                }
            }

            foreach (var id in dispatched)
            {
                pendingDispatches.Remove(id);
            }

            return Task.CompletedTask;
        }

        private async Task DispatchRequestSafeAsync(Header header, RequestPayload requestPayload,
            Dictionary<Guid, MemoryStream> streamBuffers, CancellationToken cancellationToken)
        {
            try
            {
                await DispatchRequestAsync(header, requestPayload, streamBuffers, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown — suppress
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NamedPipeProtocol: Unhandled exception dispatching request {Id}.", header.Id);
            }
        }

        private async Task DispatchRequestAsync(Header header, RequestPayload requestPayload,
            Dictionary<Guid, MemoryStream> streamBuffers, CancellationToken cancellationToken)
        {
            byte[] body = null;
            if (requestPayload.Streams is { Count: > 0 })
            {
                var streamId = Guid.Parse(requestPayload.Streams[0].Id);
                if (streamBuffers.TryGetValue(streamId, out var ms))
                {
                    body = ms.ToArray();
                    ms.Dispose();
                    streamBuffers.Remove(streamId);
                }
            }

            var request = new NamedPipeRequest
            {
                Id = header.Id,
                Verb = requestPayload.Verb,
                Path = requestPayload.Path,
                Body = body
            };

            _logger.LogDebug("NamedPipeProtocol: Dispatching {Verb} {Path} (BodyLen={Len}).",
                request.Verb, request.Path, body?.Length ?? 0);

            NamedPipeResponse response;
            if (OnRequestReceived != null)
            {
                response = await OnRequestReceived(request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                response = NamedPipeResponse.NotFound();
            }

            await SendResponseAsync(header.Id, response, cancellationToken).ConfigureAwait(false);
        }

        private void HandleResponseFrame(Header header, byte[] payload, Dictionary<Guid, MemoryStream> streamBuffers)
        {
            if (payload == null)
            {
                return;
            }

            var responsePayload = JsonSerializer.Deserialize<ResponsePayload>(payload);
            if (responsePayload == null)
            {
                return;
            }

            byte[] body = null;
            if (responsePayload.Streams is { Count: > 0 })
            {
                var streamId = Guid.Parse(responsePayload.Streams[0].Id);
                if (streamBuffers.TryGetValue(streamId, out var ms))
                {
                    body = ms.ToArray();
                    ms.Dispose();
                    streamBuffers.Remove(streamId);
                }
            }

            var receiveResponse = new ReceiveResponse { StatusCode = responsePayload.StatusCode, Body = body };

            lock (_pendingRequests)
            {
                if (_pendingRequests.TryGetValue(header.Id, out var tcs))
                {
                    tcs.TrySetResult(receiveResponse);
                    _pendingRequests.Remove(header.Id);
                }
                else
                {
                    _logger.LogWarning("NamedPipeProtocol: Received response for unknown request {Id}.", header.Id);
                }
            }
        }

        private static bool HandleStreamFrame(Header header, byte[] payload, Dictionary<Guid, MemoryStream> streamBuffers, ILogger logger)
        {
            if (payload == null || payload.Length == 0)
            {
                return true;
            }

            if (!streamBuffers.TryGetValue(header.Id, out var ms))
            {
                if (streamBuffers.Count >= MaxStreamBuffers)
                {
                    logger.LogError("NamedPipeProtocol: Maximum stream buffer count ({Max}) exceeded. Disconnecting.", MaxStreamBuffers);
                    return false;
                }

                ms = new MemoryStream();
                streamBuffers[header.Id] = ms;
            }

            ms.Write(payload);
            return true;
        }

        private async Task SendResponseAsync(Guid requestId, NamedPipeResponse response, CancellationToken cancellationToken)
        {
            var streamId = Guid.NewGuid();
            var responsePayload = new ResponsePayload
            {
                StatusCode = response.StatusCode,
                Streams = response.Body != null
                    ? new List<PayloadDescription> { new PayloadDescription { Id = streamId.ToString("D"), ContentType = "application/json", Length = response.Body.Length } }
                    : null
            };

            var payloadJson = JsonSerializer.SerializeToUtf8Bytes(responsePayload);

            await SendFrameAsync(PayloadTypes.Response, requestId, payloadJson, end: response.Body == null, cancellationToken).ConfigureAwait(false);

            if (response.Body != null)
            {
                await SendFrameAsync(PayloadTypes.Stream, streamId, response.Body, end: true, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SendFrameAsync(char type, Guid id, byte[] payload, bool end, CancellationToken cancellationToken)
        {
            var header = new Header
            {
                Type = type,
                PayloadLength = payload.Length,
                Id = id,
                End = end
            };

            var headerBytes = HeaderSerializer.Serialize(header);

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
                if (payload.Length > 0)
                {
                    await _writer.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            _cts?.Cancel();
            if (_readLoop != null)
            {
                try
                {
                    await _readLoop.ConfigureAwait(false);
                }
                catch
                {
                    // Suppress exceptions during shutdown
                }
            }

            _cts?.Dispose();
            _writeLock.Dispose();
        }
    }
}

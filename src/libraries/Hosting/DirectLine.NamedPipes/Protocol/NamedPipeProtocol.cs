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
        /// Maximum allowed payload size per frame. Mirrors
        /// <c>Microsoft.Bot.Streaming.Transport.TransportConstants.MaxLength</c>:
        /// the on-wire <c>Header.PayloadLength</c> field is encoded as 6 ASCII digits,
        /// so 999,999 is the hard ceiling enforced by the protocol itself.
        /// </summary>
        private const int MaxPayloadSize = 999_999;

        /// <summary>
        /// Maximum number of concurrent stream buffers. Limits memory usage from
        /// unmatched stream frames.
        /// </summary>
        private const int MaxStreamBuffers = 100;

        private readonly NamedPipeTransport _reader;
        private readonly NamedPipeTransport _writer;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly Dictionary<Guid, TaskCompletionSource<ReceiveResponse>> _pendingRequests = [];
        private readonly object _dispatchedTasksLock = new();
        private readonly List<Task> _dispatchedTasks = [];
        private CancellationTokenSource _cts;
        private volatile Task _readLoop;
        private int _started;

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
        /// Gets a task that completes when the read loop exits, either due to
        /// pipe disconnection, a protocol error, or disposal. Callers can await
        /// this to be signalled of disconnect without polling.
        /// </summary>
        public Task Completion => _readLoop ?? Task.CompletedTask;

        /// <summary>
        /// Start the background read loop that processes incoming frames.
        /// Must be called at most once per instance.
        /// </summary>
        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                throw new InvalidOperationException("NamedPipeProtocol has already been started.");
            }

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
                        ? [new PayloadDescription { Id = streamId.ToString("D"), ContentType = "application/json", Length = body.Length }]
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
            var completedStreams = new HashSet<Guid>();
            var pendingDispatches = new Dictionary<Guid, (Header header, RequestPayload payload)>();
            var pendingResponses = new Dictionary<Guid, ResponsePayload>();

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
                            HandleRequestFrame(header, payload, streamBuffers, completedStreams, pendingDispatches, cancellationToken);
                            break;

                        case PayloadTypes.Response:
                            HandleResponseFrame(header, payload, streamBuffers, completedStreams, pendingResponses);
                            break;

                        case PayloadTypes.Stream:
                            connected = HandleStreamFrame(header, payload, streamBuffers, completedStreams, _logger);
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
                        await TryDispatchPendingRequestsAsync(streamBuffers, completedStreams, pendingDispatches, cancellationToken).ConfigureAwait(false);
                        TryCompletePendingResponses(streamBuffers, completedStreams, pendingResponses);
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
                // Fail any pending outbound requests so callers don't wait the full timeout.
                FailPendingRequests(new IOException("Named pipe disconnected."));

                // Dispose any orphaned stream buffers to prevent memory leaks
                foreach (var ms in streamBuffers.Values)
                {
                    ms.Dispose();
                }

                streamBuffers.Clear();
                completedStreams.Clear();
                pendingDispatches.Clear();
                pendingResponses.Clear();
            }
        }

        private void FailPendingRequests(Exception exception)
        {
            List<TaskCompletionSource<ReceiveResponse>> pending;
            lock (_pendingRequests)
            {
                if (_pendingRequests.Count == 0)
                {
                    return;
                }

                pending = [.. _pendingRequests.Values];
                _pendingRequests.Clear();
            }

            foreach (var tcs in pending)
            {
                tcs.TrySetException(exception);
            }
        }

        private void HandleRequestFrame(Header header, byte[] payload,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
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
                if (!TryGetStreamId(requestPayload.Streams[0]?.Id, header.Id, out var streamId))
                {
                    return;
                }

                // Wait for the End-of-stream frame before dispatching. The Bot Framework
                // streaming protocol chunks payloads (~4096 bytes per frame), so the first
                // stream frame's arrival does not mean the body is complete.
                if (!completedStreams.Contains(streamId))
                {
                    pendingDispatches[header.Id] = (header, requestPayload);
                    return;
                }
            }

            // Extract the body synchronously on the read-loop thread so the dispatch task
            // never touches streamBuffers concurrently with the read loop.
            var body = ExtractStreamBody(header, requestPayload, streamBuffers, completedStreams);
            TrackDispatch(DispatchRequestSafeAsync(header, requestPayload, body, cancellationToken));
        }

        /// <summary>
        /// Extracts the assembled stream body (if any) for a request and removes the corresponding
        /// entry from <paramref name="streamBuffers"/> and <paramref name="completedStreams"/>.
        /// Must only be called on the read-loop thread.
        /// </summary>
        private byte[] ExtractStreamBody(Header header, RequestPayload requestPayload,
            Dictionary<Guid, MemoryStream> streamBuffers, HashSet<Guid> completedStreams)
        {
            if (requestPayload.Streams is { Count: > 0 }
                && TryGetStreamId(requestPayload.Streams[0]?.Id, header.Id, out var streamId)
                && streamBuffers.TryGetValue(streamId, out var ms))
            {
                var body = ms.ToArray();
                ms.Dispose();
                streamBuffers.Remove(streamId);
                completedStreams.Remove(streamId);
                return body;
            }

            return null;
        }

        private bool TryGetStreamId(string id, Guid requestId, out Guid streamId)
        {
            if (!Guid.TryParse(id, out streamId))
            {
                _logger.LogWarning("NamedPipeProtocol: Invalid stream id '{Id}' for request {RequestId}; ignoring frame.", id, requestId);
                return false;
            }

            return true;
        }

        private void TrackDispatch(Task task)
        {
            lock (_dispatchedTasksLock)
            {
                _dispatchedTasks.Add(task);
            }

            _ = task.ContinueWith(
                t =>
                {
                    lock (_dispatchedTasksLock)
                    {
                        _dispatchedTasks.Remove(t);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private Task TryDispatchPendingRequestsAsync(
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, (Header header, RequestPayload payload)> pendingDispatches,
            CancellationToken cancellationToken)
        {
            var dispatched = new List<Guid>();
            foreach (var (requestId, pending) in pendingDispatches)
            {
                if (pending.payload.Streams is { Count: > 0 })
                {
                    if (!TryGetStreamId(pending.payload.Streams[0]?.Id, requestId, out var streamId))
                    {
                        dispatched.Add(requestId);
                        continue;
                    }

                    if (completedStreams.Contains(streamId))
                    {
                        dispatched.Add(requestId);
                        var body = ExtractStreamBody(pending.header, pending.payload, streamBuffers, completedStreams);
                        TrackDispatch(DispatchRequestSafeAsync(pending.header, pending.payload, body, cancellationToken));
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
            byte[] body, CancellationToken cancellationToken)
        {
            try
            {
                await DispatchRequestAsync(header, requestPayload, body, cancellationToken).ConfigureAwait(false);
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
            byte[] body, CancellationToken cancellationToken)
        {
            var request = new NamedPipeRequest
            {
                Id = header.Id,
                Verb = requestPayload.Verb,
                Path = requestPayload.Path,
                Body = body
            };

            _logger.LogDebug("NamedPipeProtocol: Dispatching {Verb} {Path} (BodyLen={Len}).",
                request.Verb, request.Path, body?.Length ?? 0);

            // Capture the delegate once to avoid a torn read between the null check and invocation.
            var handler = OnRequestReceived;
            var response = handler != null
                ? await handler(request, cancellationToken).ConfigureAwait(false)
                : NamedPipeResponse.NotFound();

            await SendResponseAsync(header.Id, response, cancellationToken).ConfigureAwait(false);
        }

        private void HandleResponseFrame(Header header, byte[] payload,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, ResponsePayload> pendingResponses)
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

            if (responsePayload.Streams is { Count: > 0 })
            {
                if (!TryGetStreamId(responsePayload.Streams[0]?.Id, header.Id, out var streamId))
                {
                    CompleteResponse(header.Id, responsePayload, body: null);
                    return;
                }

                // Wait for the End-of-stream frame before completing the caller's TCS. The Bot
                // Framework streaming protocol chunks payloads (~4096 bytes per frame).
                if (!completedStreams.Contains(streamId))
                {
                    pendingResponses[header.Id] = responsePayload;
                    return;
                }
            }

            var assembledBody = ExtractResponseBody(header.Id, responsePayload, streamBuffers, completedStreams);
            CompleteResponse(header.Id, responsePayload, assembledBody);
        }

        private void TryCompletePendingResponses(
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, ResponsePayload> pendingResponses)
        {
            if (pendingResponses.Count == 0)
            {
                return;
            }

            var completed = new List<Guid>();
            foreach (var (requestId, responsePayload) in pendingResponses)
            {
                if (responsePayload.Streams is { Count: > 0 }
                    && TryGetStreamId(responsePayload.Streams[0]?.Id, requestId, out var streamId)
                    && completedStreams.Contains(streamId))
                {
                    completed.Add(requestId);
                    var body = ExtractResponseBody(requestId, responsePayload, streamBuffers, completedStreams);
                    CompleteResponse(requestId, responsePayload, body);
                }
            }

            foreach (var id in completed)
            {
                pendingResponses.Remove(id);
            }
        }

        private byte[] ExtractResponseBody(Guid requestId, ResponsePayload responsePayload,
            Dictionary<Guid, MemoryStream> streamBuffers, HashSet<Guid> completedStreams)
        {
            if (responsePayload.Streams is { Count: > 0 }
                && TryGetStreamId(responsePayload.Streams[0]?.Id, requestId, out var streamId)
                && streamBuffers.TryGetValue(streamId, out var ms))
            {
                var body = ms.ToArray();
                ms.Dispose();
                streamBuffers.Remove(streamId);
                completedStreams.Remove(streamId);
                return body;
            }

            return null;
        }

        private void CompleteResponse(Guid requestId, ResponsePayload responsePayload, byte[] body)
        {
            var receiveResponse = new ReceiveResponse { StatusCode = responsePayload.StatusCode, Body = body };

            lock (_pendingRequests)
            {
                if (_pendingRequests.TryGetValue(requestId, out var tcs))
                {
                    tcs.TrySetResult(receiveResponse);
                    _pendingRequests.Remove(requestId);
                }
                else
                {
                    _logger.LogWarning("NamedPipeProtocol: Received response for unknown request {Id}.", requestId);
                }
            }
        }

        private static bool HandleStreamFrame(Header header, byte[] payload,
            Dictionary<Guid, MemoryStream> streamBuffers, HashSet<Guid> completedStreams, ILogger logger)
        {
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

            if (payload != null && payload.Length > 0)
            {
                ms.Write(payload);
            }

            // The End flag signals that all chunks for this stream have been received.
            // Until then the body is incomplete and must not be dispatched.
            if (header.End)
            {
                completedStreams.Add(header.Id);
            }

            return true;
        }

        private async Task SendResponseAsync(Guid requestId, NamedPipeResponse response, CancellationToken cancellationToken)
        {
            var streamId = Guid.NewGuid();
            var responsePayload = new ResponsePayload
            {
                StatusCode = response.StatusCode,
                Streams = response.Body != null
                    ? [new PayloadDescription { Id = streamId.ToString("D"), ContentType = "application/json", Length = response.Body.Length }]
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
                if (!_writer.IsConnected)
                {
                    throw new IOException("Named pipe writer is disconnected.");
                }

                await _writer.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
                if (payload.Length > 0)
                {
                    await _writer.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A write failure means the outgoing pipe is broken. Cancel the read loop so
                // the hosted service observes Completion and reconnects (the old polling code
                // relied on connection.IsConnected to detect this for the outgoing pipe).
                _logger.LogWarning(ex, "NamedPipeProtocol: Write failed; tearing down protocol to trigger reconnect.");
                try { _cts?.Cancel(); } catch { /* already disposed */ }
                throw;
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

            // Defensive: if Start() was never called, the read loop's finally never ran,
            // so make sure any pending caller is released.
            FailPendingRequests(new ObjectDisposedException(nameof(NamedPipeProtocol)));

            // Wait for any in-flight dispatched request handlers to finish before
            // tearing down the write lock they depend on.
            Task[] pendingDispatches;
            lock (_dispatchedTasksLock)
            {
                pendingDispatches = _dispatchedTasks.ToArray();
            }

            if (pendingDispatches.Length > 0)
            {
                try
                {
                    await Task.WhenAll(pendingDispatches).ConfigureAwait(false);
                }
                catch
                {
                    // Individual dispatch failures are already logged via DispatchRequestSafeAsync.
                }
            }

            _cts?.Dispose();

            // Note: _writeLock (SemaphoreSlim) is intentionally not disposed. An outbound
            // SendRequestAsync captured before SetProtocol(null) may still be inside
            // SendFrameAsync at WaitAsync/Release time when DisposeAsync runs. Disposing
            // the semaphore would race with that caller and surface as ObjectDisposedException
            // on every reconnect. SemaphoreSlim only requires explicit disposal when its
            // AvailableWaitHandle has been materialized (it lazily allocates a kernel
            // handle), which this class never does — so leaving it to GC is safe.
        }
    }
}

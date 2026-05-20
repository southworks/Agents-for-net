// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    /// <summary>
    /// Verifies the protocol assembles multi-frame stream payloads correctly before dispatching
    /// — guards against the truncation regression where a request body larger than one wire chunk
    /// (~4096 bytes per the Bot Framework streaming convention) would be deserialized after only
    /// the first chunk had arrived.
    /// </summary>
    public class NamedPipeProtocolStreamingTests
    {
        private const int FrameChunkSize = 4096;

        [Theory]
        [InlineData(100)]            // single small frame
        [InlineData(FrameChunkSize)] // exactly one frame
        [InlineData(FrameChunkSize + 1)]   // two frames (boundary)
        [InlineData(10_000)]         // three frames
        [InlineData(50_000)]         // many frames
        public async Task LargeRequestBody_AssembledFromMultipleStreamFrames(int bodyLength)
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var bodyReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                bodyReceived.TrySetResult(req.Body);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var body = MakePayload(bodyLength);

            await harness.WriteRequestAsync(requestId, streamId, body.Length);
            await harness.WriteChunkedStreamAsync(streamId, body, FrameChunkSize);

            var received = await bodyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(body.Length, received.Length);
            Assert.Equal(body, received);
        }

        [Fact]
        public async Task Handler_NotInvoked_UntilEndOfStreamFlagSeen()
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var bodyReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                bodyReceived.TrySetResult(req.Body);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var body = MakePayload(10_000);

            await harness.WriteRequestAsync(requestId, streamId, body.Length);

            // Send all but the final frame — the handler MUST NOT fire yet.
            await harness.WriteChunkedStreamAsync(streamId, body[..(body.Length - 100)], FrameChunkSize, sendFinalEndFlag: false);

            var prematureDispatch = await Task.WhenAny(bodyReceived.Task, Task.Delay(300));
            Assert.NotSame(bodyReceived.Task, prematureDispatch);

            // Send the final chunk with End=true — handler should fire and see the full body.
            await harness.WriteFrameAsync(PayloadTypes.Stream, streamId, body[^100..], end: true);

            var received = await bodyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(body, received);
        }

        [Fact]
        public async Task StreamFrames_ArrivingBeforeRequestFrame_AreBuffered()
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var bodyReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                bodyReceived.TrySetResult(req.Body);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var body = MakePayload(10_000);

            // Stream frames first, then the request frame.
            await harness.WriteChunkedStreamAsync(streamId, body, FrameChunkSize);
            await harness.WriteRequestAsync(requestId, streamId, body.Length);

            var received = await bodyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(body, received);
        }

        private static byte[] MakePayload(int length)
        {
            var data = new byte[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = (byte)('a' + (i % 26));
            }

            return data;
        }

        private sealed class ProtocolHarness : IDisposable
        {
            private readonly AnonymousPipeServerStream _inboundServer;
            private readonly AnonymousPipeClientStream _inboundClient;
            private readonly AnonymousPipeServerStream _outboundServer;
            private readonly AnonymousPipeClientStream _outboundClient;
            private readonly Task _outboundDrain;
            private readonly CancellationTokenSource _drainCts;

            public NamedPipeProtocol Protocol { get; }

            private ProtocolHarness(
                AnonymousPipeServerStream inboundServer,
                AnonymousPipeClientStream inboundClient,
                AnonymousPipeServerStream outboundServer,
                AnonymousPipeClientStream outboundClient)
            {
                _inboundServer = inboundServer;
                _inboundClient = inboundClient;
                _outboundServer = outboundServer;
                _outboundClient = outboundClient;

                Protocol = new NamedPipeProtocol(
                    new NamedPipeTransport(inboundServer),
                    new NamedPipeTransport(outboundServer),
                    NullLogger.Instance);

                // Drain the outbound pipe so the protocol's response writes never block.
                _drainCts = new CancellationTokenSource();
                _outboundDrain = Task.Run(async () =>
                {
                    var buf = new byte[4096];
                    try
                    {
                        while (await outboundClient.ReadAsync(buf, _drainCts.Token).ConfigureAwait(false) > 0)
                        {
                        }
                    }
                    catch
                    {
                        // pipe closed
                    }
                });
            }

            public static Task<ProtocolHarness> CreateAsync()
            {
                var inboundServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
                var inboundClient = new AnonymousPipeClientStream(PipeDirection.Out, inboundServer.GetClientHandleAsString());
                var outboundServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
                var outboundClient = new AnonymousPipeClientStream(PipeDirection.In, outboundServer.GetClientHandleAsString());

                return Task.FromResult(new ProtocolHarness(inboundServer, inboundClient, outboundServer, outboundClient));
            }

            public void Start() => Protocol.Start();

            public Task WriteRequestAsync(Guid requestId, Guid streamId, int bodyLength)
            {
                var payload = new RequestPayload
                {
                    Verb = "POST",
                    Path = "/v3/conversations/abc/activities",
                    Streams =
                    [
                        new PayloadDescription
                        {
                            Id = streamId.ToString("D"),
                            ContentType = "application/json",
                            Length = bodyLength
                        }
                    ]
                };

                var json = JsonSerializer.SerializeToUtf8Bytes(payload);
                return WriteFrameAsync(PayloadTypes.Request, requestId, json, end: true);
            }

            public async Task WriteChunkedStreamAsync(Guid streamId, byte[] body, int chunkSize, bool sendFinalEndFlag = true)
            {
                for (int offset = 0; offset < body.Length; offset += chunkSize)
                {
                    int len = Math.Min(chunkSize, body.Length - offset);
                    bool isLast = offset + len == body.Length;
                    bool end = isLast && sendFinalEndFlag;
                    var chunk = new byte[len];
                    Buffer.BlockCopy(body, offset, chunk, 0, len);
                    await WriteFrameAsync(PayloadTypes.Stream, streamId, chunk, end).ConfigureAwait(false);
                }
            }

            public async Task WriteFrameAsync(char type, Guid id, byte[] payload, bool end)
            {
                var header = new Header
                {
                    Type = type,
                    Id = id,
                    PayloadLength = payload?.Length ?? 0,
                    End = end
                };

                var headerBytes = HeaderSerializer.Serialize(header);
                await _inboundClient.WriteAsync(headerBytes).ConfigureAwait(false);
                if (payload != null && payload.Length > 0)
                {
                    await _inboundClient.WriteAsync(payload).ConfigureAwait(false);
                }

                await _inboundClient.FlushAsync().ConfigureAwait(false);
            }

            public void Dispose()
            {
                try { Protocol.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                try { _drainCts.Cancel(); } catch { }
                try { _inboundClient.Dispose(); } catch { }
                try { _inboundServer.Dispose(); } catch { }
                try { _outboundClient.Dispose(); } catch { }
                try { _outboundServer.Dispose(); } catch { }
                _drainCts.Dispose();
            }
        }
    }
}

using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.JsonRpc;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;

namespace Microsoft.Agents.MCP.Core.Session;

public class MemoryMcpSessionManager : IMcpSessionManager
{
    private readonly IMcpPayloadFactory payloadFactory;
    private readonly ILogger<MemoryMcpSessionManager> _logger;

    public MemoryMcpSessionManager(
        IMcpPayloadFactory payloadFactory,
        ILogger<MemoryMcpSessionManager> logger)
    {
        this.payloadFactory = payloadFactory;
        _logger = logger;
    }

    public async Task<IMcpSession> CreateSessionAsync(IMcpTransport transport, CancellationToken ct)
    {
        _logger.LogInformation("Creating session for transport.");
        await Task.Yield();
        var session = new MemorySession(transport.CloseAsync, _logger);
        var id = Guid.NewGuid().ToString();
        var sessionWrapper = new McpSession(
            session.GetResponseMessagesAsync,
            session.GetIncomingMessagesAsync,
            session.WriteOutgoingPayload,
            session.Close,
            (applies, ct) =>
            {
                session.Properties = applies(session.Properties);
                return Task.CompletedTask;
            },
            () => session.Properties,
            id);
        _ = Task.Run(() => session.ListenToIncoming());
        _ = Task.Run(() => session.ListenToOutgoing());
        _ = Task.Run(() => DispatchMessagesToTransport(session, transport)); 
        await transport.Connect(id, (payload, ct) => DispatchAsync(session, payload, ct), session.Close);
        _logger.LogInformation("Session created with ID {SessionId}", id);
        return sessionWrapper;
    }

    private async Task DispatchMessagesToTransport(MemorySession session, IMcpTransport transport)
    {
        _logger.LogInformation("Dispatching messages to transport.");
        try
        {
            await foreach (var item in session.GetResponseMessagesAsync(CancellationToken.None))
            {
                var convertedPayload = payloadFactory.CreateJsonRpcPayload(item);
                await transport.SendOutgoingAsync(convertedPayload, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (session.IsClosed || transport.IsClosed)
        {
            _logger.LogWarning("Operation canceled: session or transport is closed.");
        }
    }

    private async Task DispatchAsync(MemorySession session, JsonRpcPayload payload, CancellationToken ct)
    {
        _logger.LogInformation("Dispatching payload to session.");
        if (session.IsClosed)
        {
            _logger.LogError("Session is closed. Cannot dispatch payload.");
            throw new ArgumentException("Session not found");
        }

        var parsedPayload = payloadFactory.CreatePayload(payload);
        await session.WriteIncomingPayload(parsedPayload, ct);
    }

    private record MemorySession
    {
        public CancellationTokenSource ctSource = new CancellationTokenSource();
        public McpContextProperties Properties { get; set; } = new();
        ConcurrentDictionary<StreamConsumer, bool> incomingStreamConsumers = new();
        ConcurrentDictionary<StreamConsumer, bool> outgoingStreamConsumers = new();
        private readonly ILogger _logger;

        public MemorySession(Func<CancellationToken, Task> closeAsync, ILogger logger)
        {
            this.closeAsync = closeAsync;
            _logger = logger;
        }

        private Func<CancellationToken, Task> closeAsync;
        private static readonly UnboundedChannelOptions s_channelOptions = new()
        {
            AllowSynchronousContinuations = true,
            SingleWriter = false, // typing activities may come concurrently 
            SingleReader = false,

        };

        public async Task WriteOutgoingPayload(McpPayload request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Writing outgoing payload.");
            await _outgoingQueue.Writer.WriteAsync(request, cancellationToken);
        }

        public async Task WriteIncomingPayload(McpPayload request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Writing incoming payload.");
            await _incomingQueue.Writer.WriteAsync(request, cancellationToken);
        }

        public IAsyncEnumerable<McpPayload> GetIncomingMessagesAsync(CancellationToken cancellationToken)
        {
            var consumer = new StreamConsumer(cancellationToken);
            incomingStreamConsumers.TryAdd(consumer, true);
            return consumer;
        }

        public IAsyncEnumerable<McpPayload> GetResponseMessagesAsync(CancellationToken cancellationToken)
        {
            var consumer = new StreamConsumer(cancellationToken);
            outgoingStreamConsumers.TryAdd(consumer, true);
            return consumer;
        }

        public async Task ListenToIncoming()
        {
            var asyncEnumerable = _incomingQueue.Reader.ReadAllAsync(ctSource.Token);
            await foreach (var payload in asyncEnumerable)
            {
                foreach (var consumer in incomingStreamConsumers)
                {
                    consumer.Key.SendPayload(payload);
                }
            }
        }

        public async Task ListenToOutgoing()
        {
            var asyncEnumerable = _outgoingQueue.Reader.ReadAllAsync(ctSource.Token);
            await foreach (var payload in asyncEnumerable)
            {
                foreach(var consumer in outgoingStreamConsumers)
                {
                    consumer.Key.SendPayload(payload);
                }
            }
        }

        public async Task Close(CancellationToken ct)
        {
            if (!IsClosed)
            {
                _logger.LogInformation("Closing session.");
                IsClosed = true;
                _outgoingQueue.Writer.Complete();
                _incomingQueue.Writer.Complete();
                await closeAsync.Invoke(ct);

                foreach (var consumer in incomingStreamConsumers)
                {
                    consumer.Key.Cancel();
                }

                foreach (var consumer in outgoingStreamConsumers)
                {
                    consumer.Key.Cancel();
                }

                ctSource.Cancel();

                _logger.LogInformation("Session closed.");
            }
        }

        public bool IsClosed { get; internal set; }

        private readonly Channel<McpPayload> _outgoingQueue = Channel.CreateUnbounded<McpPayload>(s_channelOptions);

        private readonly Channel<McpPayload> _incomingQueue = Channel.CreateUnbounded<McpPayload>(s_channelOptions);
    }

    private class StreamConsumer : IAsyncEnumerable<McpPayload>
    {
        public StreamConsumer(CancellationToken ct)
        {
            this.listeners = new ConcurrentDictionary<StreamEnumerable, bool>();
            this.CancellationToken = ct;
        }

        public bool IsCanceled { get; set; }
        public bool IsClosed => CancellationToken.IsCancellationRequested;
        public CancellationToken CancellationToken { get; }

        private readonly List<McpPayload> payloads = new();
        private ConcurrentDictionary<StreamEnumerable, bool> listeners;

        public IAsyncEnumerator<McpPayload> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var l = new StreamEnumerable(this, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken));
            listeners.TryAdd(l, true);
            return l;
        }

        internal void SendPayload(McpPayload payload)
        {
            payloads.Add(payload);

            foreach(var i in listeners)
            {
                i.Key.Notify();
            }

        }

        internal void Cancel()
        {
            IsCanceled = true;

            // Close the Waiting AsyncEnumerators
        }

        internal class StreamEnumerable : IAsyncEnumerator<McpPayload>
        {
            private int index = -1;
            private StreamConsumer streamConsumer;
            private readonly SemaphoreSlim waitSema = new(1,1);

            private CancellationTokenSource cancellationToken;

            public StreamEnumerable(StreamConsumer streamConsumer, CancellationTokenSource cancellationToken)
            {
                this.streamConsumer = streamConsumer;
                this.cancellationToken = cancellationToken;
            }

            public McpPayload Current
            {
                get
                {
                    if (index == -1)
                    {
                        throw new Exception("Move index before consuming");
                    }
                    if(index >= streamConsumer.payloads.Count)
                    {
                        throw new Exception("End of stream");
                    }

                    return streamConsumer.payloads[index];
                }
            }

            public ValueTask DisposeAsync()
            {
                streamConsumer.listeners.TryRemove(this, out _);
                cancellationToken.Dispose();
                return ValueTask.CompletedTask;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                index++;
                if(index < streamConsumer.payloads.Count)
                {
                    return true;
                }    

                while (index >= streamConsumer.payloads.Count)
                {
                    // Wait for more payloads.
                    // TODO: Could be a race condition if we wait AFTER the release happened, causing it to lock up.
                    await waitSema.WaitAsync(index, cancellationToken.Token);
                }

                return !cancellationToken.IsCancellationRequested;
            }

            internal void Notify()
            {
                try
                {
                    waitSema.Release();
                } catch(SemaphoreFullException)
                {

                }
                
            }
        }
    }
}

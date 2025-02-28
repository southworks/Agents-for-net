using System.Threading.Channels;
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.JsonRpc;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Extensions.Logging;

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
        _ = DispatchMessagesToTransport(session, transport).ConfigureAwait(false);
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
        public McpContextProperties Properties { get; set; } = new();
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

        public IAsyncEnumerable<McpPayload> GetResponseMessagesAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting response messages.");
            return _outgoingQueue.Reader.ReadAllAsync(cancellationToken);
        }

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
            _logger.LogInformation("Getting incoming messages.");
            return _incomingQueue.Reader.ReadAllAsync(cancellationToken);
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
                _logger.LogInformation("Session closed.");
            }
        }

        public bool IsClosed { get; internal set; }

        private readonly Channel<McpPayload> _outgoingQueue = Channel.CreateUnbounded<McpPayload>(s_channelOptions);

        private readonly Channel<McpPayload> _incomingQueue = Channel.CreateUnbounded<McpPayload>(s_channelOptions);
    }
}

using Microsoft.Agents.MCP.Core.JsonRpc;
using System.Net.Http.Json;
using Microsoft.Agents.MCP.Core.Abstractions;

namespace Microsoft.Agents.MCP.Client.Transports
{
    /// <summary>
    /// Represents an HTTP callback client transport.
    /// </summary>
    public class HttpSseClientTransport : IMcpTransport
    {
        public string? Endpoint { get; private set; }

        private Func<JsonRpcPayload, CancellationToken, Task>? ingestionFunc;
        private bool closed = false;
        private readonly string initializationEndpoint;
        private readonly IHttpClientFactory httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpCallbackServerTransport"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="initializationEndpoint">The endpoint to call.</param>
        public HttpSseClientTransport(
            string initializationEndpoint,
            IHttpClientFactory httpClientFactory)
        {
            this.initializationEndpoint = initializationEndpoint;
            this.httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Gets a value indicating whether the transport is closed.
        /// </summary>
        public bool IsClosed => closed;

        /// <summary>
        /// Closes the transport asynchronously.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous close operation.</returns>
        public Task CloseAsync(CancellationToken ct)
        {
            closed = true;
            // TODO: Close connection
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects the transport.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="ingestMessage">The function to ingest messages.</param>
        /// <param name="close">The function to close the transport.</param>
        /// <returns>A task that represents the asynchronous connect operation.</returns>
        public Task Connect(string sessionId, Func<JsonRpcPayload, CancellationToken, Task> ingestMessage, Func<CancellationToken, Task> close)
        {
            // Call endpoint to connect to
            // use socket for streaming
            // register endpoint for messages
            using var client = httpClientFactory.CreateClient();
            //await client.PostAsJsonAsync(initializationEndpoint, payload, ct);

            Endpoint = "";
            ingestionFunc = ingestMessage;

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a payload to the client asynchronously.
        /// </summary>
        /// <param name="payload">The JSON-RPC payload.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        public async Task SendOutgoingAsync(JsonRpcPayload payload, CancellationToken ct)
        {
            using var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync(Endpoint, payload, ct);
        }

        /// <summary>
        /// Sends a payload to the server asynchronously.
        /// </summary>
        /// <param name="payload">The JSON-RPC payload.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        public Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken ct) => ingestionFunc?.Invoke(payload, ct) ?? throw new Exception("Transport has not been initialized");
    }
}

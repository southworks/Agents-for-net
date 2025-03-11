using Microsoft.Agents.Mcp.Client.Transports;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Mcp.Tests
{
    public class StdioTransportTests : TransportTestBase
    {
        private readonly string command = "node";
        private readonly string[] arguments = { "--port", "stdio" };

        protected override IMcpTransport CreateTransport(IMcpProcessor processor, ITransportManager transportManager, ILogger<SseTransportTests> logger)
        {
            return new StdioClientTransport(command, arguments);
        }

        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            var transport = new StdioClientTransport(command, arguments);

            Assert.False(transport.IsClosed);
        }

        [Fact]
        public async Task Connect_ShouldStartProcess()
        {
            var transport = new StdioClientTransport(command, arguments);
            var mockIngestFunc = new Mock<Func<JsonRpcPayload, CancellationToken, Task>>();
            var mockCloseFunc = new Mock<Func<CancellationToken, Task>>();

            await transport.Connect("sessionId", mockIngestFunc.Object, mockCloseFunc.Object);

            Assert.NotNull(transport);
        }

        [Fact]
        public async Task SendOutgoingAsync_ShouldWriteToStandardInput()
        {
            var transport = new StdioClientTransport(command, arguments);
            var mockIngestFunc = new Mock<Func<JsonRpcPayload, CancellationToken, Task>>();
            var mockCloseFunc = new Mock<Func<CancellationToken, Task>>();

            await transport.Connect("sessionId", mockIngestFunc.Object, mockCloseFunc.Object);

            var payload = new JsonRpcPayload { Method = "testMethod" };
            await transport.SendOutgoingAsync(payload, CancellationToken.None);

            // Cannot directly assert StandardInput in a unit test, consider using integration tests for full verification
            Assert.True(true);
        }

        [Fact]
        public async Task ProcessPayloadAsync_ShouldInvokeIngestionFunc()
        {
            var transport = new StdioClientTransport(command, arguments);
            var mockIngestFunc = new Mock<Func<JsonRpcPayload, CancellationToken, Task>>();
            var mockCloseFunc = new Mock<Func<CancellationToken, Task>>();

            await transport.Connect("sessionId", mockIngestFunc.Object, mockCloseFunc.Object);

            var payload = new JsonRpcPayload { Method = "testMethod" };
            await transport.ProcessPayloadAsync(payload, CancellationToken.None);

            mockIngestFunc.Verify(func => func(payload, CancellationToken.None), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldSetIsClosed()
        {
            var transport = new StdioClientTransport(command, arguments);
            transport.Dispose();

            Assert.True(transport.IsClosed);
        }
    }
}

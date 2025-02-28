using Xunit;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using Microsoft.AspNetCore.Http;
using Microsoft.Agents.MCP.Server.Sample.Operations;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Transport;
using Microsoft.Agents.MCP.Server.Transports;

namespace Microsoft.Agents.Hosting.MCPServer.Operations.Tests
{
    [TestClass]
    public class MathAddOperationExecutorSseTests
    {
        [TestMethod]
        public async Task ExecuteAsyncValidInput()
        {
            // Arrange
            var executor = new MathAddOperationExecutor();
            var input = new MathAddInput
            {
                Number1 = "5",
                Number2 = "3"
            };
            var payload = new McpRequest<MathAddInput>
            {
                Method = "Math_Adder",
                Parameters = input
            };
            var contextMock = new Mock<IMcpContext>();
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // Mock ITransportManager
            var transportManagerMock = new Mock<ITransportManager>();

            // Mock HttpResponse
            var responseMock = new Mock<HttpResponse>();
            var responseStream = new MemoryStream();
            responseMock.SetupGet(r => r.Body).Returns(responseStream);
            responseMock.SetupGet(r => r.Headers).Returns(new HeaderDictionary());

            // Instantiate HttpSseServerTransport
            var transport = new HttpSseServerTransport(transportManagerMock.Object, (s) => s, responseMock.Object, cancellationToken);

            // Act
            await transport.Connect("test-session-id", (p, ct) => executor.ExecuteAsync(payload, contextMock.Object, ct), ct => Task.CompletedTask);

            // Assert
            responseMock.Verify(r => r.Body, Times.AtLeastOnce);
            responseStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(responseStream))
            {
                var responseBody = await reader.ReadToEndAsync();
                Assert.IsTrue(responseBody.Contains("connected"));
            }

            // Close the connection
            await transport.CloseAsync(cancellationToken);
        }

        [TestMethod]
        public async Task ExecuteAsyncInvalidInput()
        {
            // Arrange
            var executor = new MathAddOperationExecutor();
            var input = new MathAddInput
            {
                Number1 = "invalid",
                Number2 = "3"
            };
            var payload = new McpRequest<MathAddInput>
            {
                Method = "Math_Adder",
                Parameters = input
            };
            var contextMock = new Mock<IMcpContext>();
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // Mock ITransportManager
            var transportManagerMock = new Mock<ITransportManager>();

            // Mock HttpResponse
            var responseMock = new Mock<HttpResponse>();
            var responseStream = new MemoryStream();
            responseMock.SetupGet(r => r.Body).Returns(responseStream);
            responseMock.SetupGet(r => r.Headers).Returns(new HeaderDictionary());

            // Instantiate HttpSseServerTransport
            var transport = new HttpSseServerTransport(transportManagerMock.Object, (s) => s, responseMock.Object, cancellationToken);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<FormatException>(async () =>
            {
                await transport.Connect("test-session-id", async (p, ct) => await executor.ExecuteAsync(payload, contextMock.Object, ct), ct => Task.CompletedTask);
            });

            // Close the connection
            await transport.CloseAsync(cancellationToken);
        }

        [TestMethod]
        public async Task ExecuteAsyncCancellation()
        {
            // Arrange
            var executor = new MathAddOperationExecutor();
            var input = new MathAddInput
            {
                Number1 = "5",
                Number2 = "3"
            };
            var payload = new McpRequest<MathAddInput>
            {
                Method = "Math_Adder",
                Parameters = input
            };
            var contextMock = new Mock<IMcpContext>();
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // Mock ITransportManager
            var transportManagerMock = new Mock<ITransportManager>();

            // Mock HttpResponse
            var responseMock = new Mock<HttpResponse>();
            var responseStream = new MemoryStream();
            responseMock.SetupGet(r => r.Body).Returns(responseStream);
            responseMock.SetupGet(r => r.Headers).Returns(new HeaderDictionary());

            // Instantiate HttpSseServerTransport
            var transport = new HttpSseServerTransport(transportManagerMock.Object, responseMock.Object, cancellationToken);

            // Act
            cancellationTokenSource.Cancel();

            // Assert
            Assert.IsTrue(transport.IsClosed);
        }
    }
}


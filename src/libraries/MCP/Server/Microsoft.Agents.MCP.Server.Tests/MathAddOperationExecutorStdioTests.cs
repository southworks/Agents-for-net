using Moq;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Agents.MCP.Server.Sample.Operations;
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Server.Transports;

namespace Microsoft.Agents.MCP.Server.Tests
{
    [TestClass]
    public class MathAddOperationExecutorStdioTests
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
            var cancellationToken = CancellationToken.None;

            // Simulate stdio
            var inputStream = new MemoryStream();
            var outputStream = new MemoryStream();
            var transport = new StdioServerTransport(inputStream, outputStream);

            // Write input to the input stream
            var inputJson = JsonSerializer.Serialize(payload);
            var inputBytes = Encoding.UTF8.GetBytes(inputJson);
            await inputStream.WriteAsync(inputBytes, 0, inputBytes.Length);
            inputStream.Position = 0;

            // Act
            var result = await executor.ExecuteAsync(payload, contextMock.Object, cancellationToken);

            // Read output from the output stream
            outputStream.Position = 0;
            var outputBytes = new byte[outputStream.Length];
            await outputStream.ReadAsync(outputBytes, 0, outputBytes.Length);
            var outputJson = Encoding.UTF8.GetString(outputBytes).Trim('\uFEFF');

            var output = JsonSerializer.Deserialize<MathAddOutput>(outputJson);

            // Assert
            Assert.IsNotNull(output);
            Assert.AreEqual(8, output.Total);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public async Task ExecuteAsyncInvalidInput()
        {
            // Arrange
            var executor = new MathAddOperationExecutor();
            var input = new MathAddInput
            {
                Number1 = "test",
                Number2 = "3"
            };
            var payload = new McpRequest<MathAddInput>
            {
                Method = "Math_Adder",
                Parameters = input
            };
            var contextMock = new Mock<IMcpContext>();
            var cancellationToken = CancellationToken.None;

            // Simulate stdio
            var inputStream = new MemoryStream();
            var outputStream = new MemoryStream();
            var transport = new StdioServerTransport(inputStream, outputStream);

            // Write input to the input stream
            var inputJson = JsonSerializer.Serialize(payload);
            var inputBytes = Encoding.UTF8.GetBytes(inputJson);
            await inputStream.WriteAsync(inputBytes, 0, inputBytes.Length);
            inputStream.Position = 0;

            // Act
            await executor.ExecuteAsync(payload, contextMock.Object, cancellationToken);

            // Assert is handled by ExpectedException
        }
    }
}

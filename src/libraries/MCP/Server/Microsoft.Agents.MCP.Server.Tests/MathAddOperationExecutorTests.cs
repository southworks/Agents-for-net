using Xunit;
using Microsoft.Agents.Hosting.MCP.Server.Contracts;
using Microsoft.Agents.Hosting.MCP.Server.Entities.Payloads;
using Moq;
using Microsoft.Agents.Hosting.MCPServer.Operations;

namespace Microsoft.Agents.Hosting.MCPServer.Tests;

public class MathAddOperationExecutorTests
{
    private readonly MathAddOperationExecutor _executor;

    public MathAddOperationExecutorTests()
    {
        _executor = new MathAddOperationExecutor();
    }

    [Fact]
    public async Task ExecuteAsync_ValidInput_ReturnsCorrectSum()
    {
        // Arrange
        var input = new MathAddInput { Number1 = "5", Number2 = "3" };
        var request = McpRequest<MathAddInput>.CreateFrom(new McpRequest(), input);
        var context = new Mock<IMcpContext>().Object;
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _executor.ExecuteAsync(request, context, cancellationToken);

        // Assert
        Assert.Equal(8, result.Total);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidInput_ThrowsFormatException()
    {
        // Arrange
        var input = new MathAddInput { Number1 = "abc", Number2 = "3" };
        var request = McpRequest<MathAddInput>.CreateFrom(new McpRequest(), input);
        var context = new Mock<IMcpContext>().Object;
        var cancellationToken = CancellationToken.None;

        // Act & Assert
        await Assert.ThrowsAsync<FormatException>(() => _executor.ExecuteAsync(request, context, cancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_EdgeCase_LargeNumbers_ReturnsCorrectSum()
    {
        // Arrange
        var input = new MathAddInput { Number1 = int.MaxValue.ToString(), Number2 = "1" };
        var request = McpRequest<MathAddInput>.CreateFrom(new McpRequest(), input);
        var context = new Mock<IMcpContext>().Object;
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _executor.ExecuteAsync(request, context, cancellationToken);

        // Assert
        Assert.Equal(int.MinValue, result.Total); // Overflow behavior
    }
}

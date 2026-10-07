using FluentAssertions;
using Harness.Models;
using Harness.Tools;
using Xunit;

namespace Harness.Tools.Tests;

public class ToolsUnitTests
{
    [Fact]
    public async Task GetServerStatusTool_ShouldReturnHealthyStatus()
    {
        var tool = new GetServerStatusTool();
        var result = await tool.ExecuteAsync("{}");

        result.IsSuccess.Should().BeTrue();
        result.RiskLevel.Should().Be(ToolRiskLevel.LOW);
        result.OutputJson.Should().Contain("HEALTHY");
        result.OutputJson.Should().Contain("cpuUsagePercent");
    }

    [Fact]
    public async Task CalculatorTool_ShouldAddNumbersCorrectly()
    {
        var tool = new CalculatorTool();
        var result = await tool.ExecuteAsync("{\"operation\": \"add\", \"a\": 10, \"b\": 25}");

        result.IsSuccess.Should().BeTrue();
        result.OutputJson.Should().Contain("\"result\":35");
    }

    [Fact]
    public async Task CalculatorTool_WhenDividingByZero_ShouldFail()
    {
        var tool = new CalculatorTool();
        var result = await tool.ExecuteAsync("{\"operation\": \"divide\", \"a\": 10, \"b\": 0}");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zero");
    }

    [Fact]
    public async Task SearchDocumentationTool_ShouldFindDatabaseArticle()
    {
        var tool = new SearchDocumentationTool();
        var result = await tool.ExecuteAsync("{\"query\": \"database connection\"}");

        result.IsSuccess.Should().BeTrue();
        result.OutputJson.Should().Contain("DOC-001");
        result.OutputJson.Should().Contain("Database Connection Failures");
    }

    [Fact]
    public async Task GetSystemLogsTool_ShouldFilterErrors()
    {
        var tool = new GetSystemLogsTool();
        var result = await tool.ExecuteAsync("{\"level\": \"error\", \"limit\": 5}");

        result.IsSuccess.Should().BeTrue();
        result.RiskLevel.Should().Be(ToolRiskLevel.MEDIUM);
        result.OutputJson.Should().Contain("ERROR");
    }

    [Fact]
    public async Task FlakySimulatorTool_ShouldFailFirstThenSucceedOnRetry()
    {
        FlakySimulatorTool.ResetCounter();
        var tool = new FlakySimulatorTool();

        var res1 = await tool.ExecuteAsync("{}");
        res1.IsSuccess.Should().BeFalse();
        res1.ErrorMessage.Should().Contain("504 Gateway Timeout");

        var res2 = await tool.ExecuteAsync("{}");
        res2.IsSuccess.Should().BeTrue();
        res2.OutputJson.Should().Contain("RECOVERED");
    }

    [Fact]
    public async Task DeleteClusterTool_ShouldAlwaysBlock()
    {
        var tool = new DeleteClusterTool();
        tool.RiskLevel.Should().Be(ToolRiskLevel.CRITICAL);

        var result = await tool.ExecuteAsync("{\"target\": \"prod-db\"}");
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ToolExecutionStatus.Blocked);
    }
}

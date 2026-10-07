using System.Text.Json;
using FluentAssertions;
using Harness.Core.Interfaces;
using Harness.Models;
using NSubstitute;
using Xunit;

namespace Harness.Api.Tests;

public class ApiUnitTests
{
    [Fact]
    public void AgentResponse_ShouldSerializeCorrectly()
    {
        var response = new AgentResponse
        {
            RunId = "run-test",
            ConversationId = "conv-test",
            FinalAnswer = "Verified answer",
            Status = RunStatus.Completed,
            LatencyMs = 120.5,
            LoopCount = 2,
            Steps = new List<AgentStep>
            {
                new() { Type = AgentStepType.PLAN, Status = "Success" },
                new() { Type = AgentStepType.COMPLETE, Status = "Success" }
            }
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().Contain("run-test");
        json.Should().Contain("Verified answer");
        json.Should().Contain("Completed");
    }

    [Fact]
    public void ToolDefinition_ShouldHoldSchema()
    {
        var toolDef = new ToolDefinition
        {
            Name = "test_tool",
            Description = "desc",
            RiskLevel = ToolRiskLevel.LOW,
            InputSchema = JsonDocument.Parse("{\"type\":\"object\"}")
        };

        toolDef.Name.Should().Be("test_tool");
        toolDef.RiskLevel.Should().Be(ToolRiskLevel.LOW);
        toolDef.InputSchema.RootElement.GetProperty("type").GetString().Should().Be("object");
    }
}

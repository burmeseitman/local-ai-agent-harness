using System.Text.Json;
using FluentAssertions;
using Harness.Core.Interfaces;
using Harness.Core.Tools;
using Harness.Models;
using Xunit;

namespace Harness.Core.Tests;

public class ToolRegistryTests
{
    private class DummyTool : ITool
    {
        public string Name => "dummy_tool";
        public string Description => "A test tool";
        public ToolRiskLevel RiskLevel => ToolRiskLevel.LOW;
        public JsonDocument InputSchema => JsonDocument.Parse("{}");
        public JsonDocument OutputSchema => JsonDocument.Parse("{}");
        public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolResult { ToolName = Name, IsSuccess = true });
    }

    [Fact]
    public void RegisterTool_ShouldStoreAndRetrieveTool()
    {
        var registry = new ToolRegistry();
        var tool = new DummyTool();

        registry.RegisterTool(tool);

        registry.HasTool("dummy_tool").Should().BeTrue();
        registry.GetTool("dummy_tool").Should().BeSameAs(tool);
        registry.GetTool("DUMMY_TOOL").Should().BeSameAs(tool); // Case-insensitive
    }

    [Fact]
    public void GetTool_WhenNotRegistered_ShouldReturnNull()
    {
        var registry = new ToolRegistry();
        registry.GetTool("non_existent").Should().BeNull();
        registry.HasTool("non_existent").Should().BeFalse();
    }

    [Fact]
    public void GetDefinitions_ShouldReturnAllToolSchemas()
    {
        var registry = new ToolRegistry(new[] { new DummyTool() });
        var defs = registry.GetDefinitions();

        defs.Should().HaveCount(1);
        defs[0].Name.Should().Be("dummy_tool");
        defs[0].RiskLevel.Should().Be(ToolRiskLevel.LOW);
    }
}

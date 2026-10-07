using FluentAssertions;
using Harness.Core.Context;
using Harness.Core.Interfaces;
using Harness.Models;
using NSubstitute;
using Xunit;

namespace Harness.Core.Tests;

public class ContextBuilderTests
{
    private readonly IMemoryService _memoryService = Substitute.For<IMemoryService>();
    private readonly IConversationManager _conversationManager = Substitute.For<IConversationManager>();

    [Fact]
    public async Task BuildContextAsync_ShouldIncludeSystemPromptAndRelevantMemory()
    {
        _memoryService.SearchMemoryAsync("database failure", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryItem>
            {
                new() { Id = "m-1", Content = "Database connection retry rule", Importance = 4.0 }
            });

        _conversationManager.GetHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentMessage>());

        var builder = new ContextBuilder(_memoryService, _conversationManager);
        var tools = new List<ToolDefinition>
        {
            new() { Name = "test_tool", Description = "A test tool", RiskLevel = ToolRiskLevel.LOW }
        };

        var items = await builder.BuildContextAsync("database failure", "conv-1", tools);

        items.Should().Contain(i => i.Type == "SystemPrompt");
        items.Should().Contain(i => i.Type == "Memory" && i.Content.Contains("Database connection retry rule"));
        items.Should().Contain(i => i.Type == "ToolCatalog");
        items.Should().Contain(i => i.Type == "CurrentTask");
    }

    [Fact]
    public async Task BuildContextAsync_WhenNoMemoryMatches_ShouldNotIncludeMemoryContextItems()
    {
        _memoryService.SearchMemoryAsync("unknown query", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryItem>());

        _conversationManager.GetHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentMessage>());

        var builder = new ContextBuilder(_memoryService, _conversationManager);
        var items = await builder.BuildContextAsync("unknown query", "conv-1", new List<ToolDefinition>());

        items.Should().NotContain(i => i.Type == "Memory");
    }
}

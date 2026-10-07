using FluentAssertions;
using Harness.Core.Context;
using Harness.Core.Interfaces;
using Harness.Core.Loop;
using Harness.Core.Runner;
using Harness.Core.Tools;
using Harness.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Harness.Core.Tests;

public class HarnessRunnerTests
{
    [Fact]
    public async Task RunAsync_ShouldExecuteFullPipelineSuccessfully()
    {
        var model = Substitute.For<IAiModelProvider>();
        var executor = Substitute.For<IToolExecutor>();
        var tracer = Substitute.For<IExecutionTracer>();
        var memory = Substitute.For<IMemoryService>();
        var convManager = Substitute.For<IConversationManager>();

        convManager.GetOrCreateConversationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Conversation { Id = "test-conv-1" });

        convManager.GetHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentMessage>());

        memory.SearchMemoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryItem>());

        model.GenerateAsync(Arg.Any<List<AgentMessage>>(), Arg.Any<List<ToolDefinition>>(), Arg.Any<CancellationToken>())
            .Returns(new ModelCompletionResult
            {
                Content = "Direct answer without tools.",
                ToolCalls = new List<ToolCall>()
            });

        var contextBuilder = new ContextBuilder(memory, convManager);
        var toolRegistry = new ToolRegistry();
        var agentLoop = new AgentLoop(model, executor, tracer, NullLogger<AgentLoop>.Instance);

        var runner = new HarnessRunner(
            contextBuilder,
            toolRegistry,
            agentLoop,
            memory,
            convManager,
            tracer,
            NullLogger<HarnessRunner>.Instance);

        var request = new AgentRequest
        {
            Prompt = "Hello Antigravity Harness",
            ConversationId = "test-conv-1"
        };

        var response = await runner.RunAsync(request);

        response.Should().NotBeNull();
        response.RunId.Should().NotBeNullOrEmpty();
        response.ConversationId.Should().Be("test-conv-1");
        response.Status.Should().Be(RunStatus.Completed);
        response.FinalAnswer.Should().Be("Direct answer without tools.");
        response.Steps.Should().NotBeEmpty();
    }
}

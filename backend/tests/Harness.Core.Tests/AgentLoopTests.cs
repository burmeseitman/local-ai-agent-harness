using FluentAssertions;
using Harness.Core.Interfaces;
using Harness.Core.Loop;
using Harness.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Harness.Core.Tests;

public class AgentLoopTests
{
    private readonly IAiModelProvider _model = Substitute.For<IAiModelProvider>();
    private readonly IToolExecutor _executor = Substitute.For<IToolExecutor>();
    private readonly IExecutionTracer _tracer = Substitute.For<IExecutionTracer>();

    [Fact]
    public async Task ExecuteAsync_WhenToolAllowed_ShouldExecuteAndComplete()
    {
        // Turn 1: Model requests tool
        _model.GenerateAsync(Arg.Any<List<AgentMessage>>(), Arg.Any<List<ToolDefinition>>(), Arg.Any<CancellationToken>())
            .Returns(
                new ModelCompletionResult
                {
                    Content = "Need server status",
                    ToolCalls = new List<ToolCall> { new() { ToolName = "get_server_status", ArgumentsJson = "{}" } }
                },
                new ModelCompletionResult
                {
                    Content = "The server is healthy.",
                    ToolCalls = new List<ToolCall>() // Finished
                }
            );

        _executor.ExecuteToolCallAsync(Arg.Any<ToolCall>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult
            {
                ToolName = "get_server_status",
                IsSuccess = true,
                OutputJson = "{\"status\": \"HEALTHY\"}",
                Status = ToolExecutionStatus.Success
            });

        var loop = new AgentLoop(_model, _executor, _tracer, NullLogger<AgentLoop>.Instance);
        var state = new AgentState
        {
            UserPrompt = "Check server",
            AvailableTools = new List<ToolDefinition>()
        };

        var finalState = await loop.ExecuteAsync(state);

        finalState.Status.Should().Be(RunStatus.Completed);
        finalState.FinalAnswer.Should().Be("The server is healthy.");
        finalState.Steps.Should().Contain(s => s.Type == AgentStepType.ACT);
        finalState.Steps.Should().Contain(s => s.Type == AgentStepType.OBSERVE);
        finalState.Steps.Should().Contain(s => s.Type == AgentStepType.COMPLETE);
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolBlocked_ShouldTerminateWithGuardrailMessage()
    {
        _model.GenerateAsync(Arg.Any<List<AgentMessage>>(), Arg.Any<List<ToolDefinition>>(), Arg.Any<CancellationToken>())
            .Returns(new ModelCompletionResult
            {
                Content = "Attempting cluster delete",
                ToolCalls = new List<ToolCall> { new() { ToolName = "delete_server_cluster", ArgumentsJson = "{}" } }
            });

        _executor.ExecuteToolCallAsync(Arg.Any<ToolCall>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult
            {
                ToolName = "delete_server_cluster",
                IsSuccess = false,
                ErrorMessage = "Security Policy Violation: CRITICAL risk is blocked",
                Status = ToolExecutionStatus.Blocked
            });

        var loop = new AgentLoop(_model, _executor, _tracer, NullLogger<AgentLoop>.Instance);
        var state = new AgentState { UserPrompt = "Delete cluster" };

        var finalState = await loop.ExecuteAsync(state);

        finalState.Status.Should().Be(RunStatus.Completed);
        finalState.FinalAnswer.Should().Contain("Harness Security Guardrail");
        finalState.FinalAnswer.Should().Contain("blocked");
    }

    [Fact]
    public async Task ExecuteAsync_WhenLoopExceedsMaxIterations_ShouldFailSafely()
    {
        // Model keeps repeating tool calls infinitely
        _model.GenerateAsync(Arg.Any<List<AgentMessage>>(), Arg.Any<List<ToolDefinition>>(), Arg.Any<CancellationToken>())
            .Returns(new ModelCompletionResult
            {
                Content = "Thinking...",
                ToolCalls = new List<ToolCall> { new() { ToolName = "dummy", ArgumentsJson = "{}" } }
            });

        _executor.ExecuteToolCallAsync(Arg.Any<ToolCall>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult { ToolName = "dummy", IsSuccess = true, OutputJson = "{}" });

        var loop = new AgentLoop(_model, _executor, _tracer, NullLogger<AgentLoop>.Instance);
        var state = new AgentState { UserPrompt = "Infinite loop test", MaxIterations = 3 };

        var finalState = await loop.ExecuteAsync(state);

        finalState.Status.Should().Be(RunStatus.Failed);
        finalState.ErrorMessage.Should().Contain("maximum loop iterations");
        finalState.CurrentIteration.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var loop = new AgentLoop(_model, _executor, _tracer, NullLogger<AgentLoop>.Instance);
        var state = new AgentState { UserPrompt = "Cancel test" };

        var act = async () => await loop.ExecuteAsync(state, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

using System.Diagnostics;
using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;
using Microsoft.Extensions.Logging;

namespace Harness.Core.Loop;

public class AgentState
{
    public string RunId { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public int CurrentIteration { get; set; } = 0;
    public int MaxIterations { get; set; } = 10;
    public RunStatus Status { get; set; } = RunStatus.Running;
    public List<AgentMessage> Messages { get; set; } = new();
    public List<ToolDefinition> AvailableTools { get; set; } = new();
    public ToolCall? PendingToolCall { get; set; }
    public ToolResult? LastToolResult { get; set; }
    public string? FinalAnswer { get; set; }
    public string? ErrorMessage { get; set; }
    public List<AgentStep> Steps { get; set; } = new();
}

public class AgentLoop
{
    private readonly IAiModelProvider _modelProvider;
    private readonly IToolExecutor _toolExecutor;
    private readonly IExecutionTracer _tracer;
    private readonly ILogger<AgentLoop> _logger;

    public AgentLoop(
        IAiModelProvider modelProvider,
        IToolExecutor toolExecutor,
        IExecutionTracer tracer,
        ILogger<AgentLoop> logger)
    {
        _modelProvider = modelProvider;
        _toolExecutor = toolExecutor;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task<AgentState> ExecuteAsync(
        AgentState state,
        Action<AgentStep>? onStep = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting AgentLoop for Run {RunId}, User Prompt: '{Prompt}'", state.RunId, state.UserPrompt);

        while (state.CurrentIteration < state.MaxIterations && state.Status == RunStatus.Running)
        {
            state.CurrentIteration++;
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("AgentLoop Iteration {Iteration}/{Max} for Run {RunId}",
                state.CurrentIteration, state.MaxIterations, state.RunId);

            // ==========================================
            // 1. NODE: PLAN
            // ==========================================
            var planStep = await RecordStepAsync(state, AgentStepType.PLAN, "Plan Execution Step",
                $"Iteration {state.CurrentIteration}: Synthesizing context and determining next strategic action.",
                state.UserPrompt, async () =>
                {
                    var completion = await _modelProvider.GenerateAsync(state.Messages, state.AvailableTools, cancellationToken);
                    return completion;
                }, onStep);

            var modelCompletion = (ModelCompletionResult?)planStep.ResultObject;
            if (modelCompletion == null)
            {
                state.Status = RunStatus.Failed;
                state.ErrorMessage = "Model completion returned null.";
                break;
            }

            // If the model did NOT request any tool, it has reached its final answer!
            if (!modelCompletion.HasToolCalls)
            {
                state.FinalAnswer = modelCompletion.Content;
                state.Status = RunStatus.Completed;

                // ==========================================
                // NODE: COMPLETE
                // ==========================================
                await RecordStepAsync<object?>(state, AgentStepType.COMPLETE, "Task Completed",
                    "Agent synthesized verified conclusion and completed execution cycle.",
                    state.FinalAnswer, () => Task.FromResult<object?>(null), onStep);

                break;
            }

            // ==========================================
            // 2. NODE: ACT
            // ==========================================
            var targetCall = modelCompletion.ToolCalls[0];
            state.PendingToolCall = targetCall;

            var actStep = await RecordStepAsync(state, AgentStepType.ACT, $"Request Tool: {targetCall.ToolName}",
                $"Agent selected tool '{targetCall.ToolName}' with arguments: {targetCall.ArgumentsJson}",
                targetCall.ArgumentsJson,
                () => Task.FromResult(targetCall), onStep);

            // ==========================================
            // 3. NODE: OBSERVE
            // ==========================================
            var observeStep = await RecordStepAsync(state, AgentStepType.OBSERVE, $"Execute Tool: {targetCall.ToolName}",
                $"Dispatched tool call to Harness Policy Engine & Tool Executor.",
                targetCall.ArgumentsJson, async () =>
                {
                    return await _toolExecutor.ExecuteToolCallAsync(targetCall, state.RunId, cancellationToken);
                }, onStep);

            var toolResult = (ToolResult?)observeStep.ResultObject;
            state.LastToolResult = toolResult;

            // Update conversation working memory with tool output
            var toolMessageContent = toolResult != null && toolResult.IsSuccess
                ? toolResult.OutputJson
                : $"Tool execution failed or was blocked: {toolResult?.ErrorMessage}";

            state.Messages.Add(new AgentMessage
            {
                ConversationId = state.ConversationId,
                Role = MessageRole.TOOL,
                Content = toolMessageContent,
                Metadata = new Dictionary<string, string>
                {
                    ["ToolName"] = targetCall.ToolName,
                    ["Status"] = toolResult?.Status.ToString() ?? "Unknown"
                }
            });

            // ==========================================
            // 4. NODE: EVALUATE
            // ==========================================
            var evalStep = await RecordStepAsync(state, AgentStepType.EVALUATE, "Evaluate Tool Result",
                $"Evaluated result from {targetCall.ToolName}. Success: {toolResult?.IsSuccess}, Status: {toolResult?.Status}",
                toolMessageContent,
                () => Task.FromResult(toolResult), onStep);

            // If the tool was blocked by policy, generate a safe explanatory finish
            if (toolResult != null && toolResult.Status == ToolExecutionStatus.Blocked)
            {
                state.FinalAnswer = $"Harness Security Guardrail: Action '{targetCall.ToolName}' was blocked. Reason: {toolResult.ErrorMessage}";
                state.Status = RunStatus.Completed;

                await RecordStepAsync<object?>(state, AgentStepType.COMPLETE, "Safely Guarded Completion",
                    "Harness terminated run safely after enforcing security policy restriction.",
                    state.FinalAnswer, () => Task.FromResult<object?>(null), onStep);

                break;
            }
        }

        // Check if loop exceeded max iterations without finishing
        if (state.CurrentIteration >= state.MaxIterations && state.Status == RunStatus.Running)
        {
            state.Status = RunStatus.Failed;
            state.ErrorMessage = $"Agent exceeded maximum loop iterations limit ({state.MaxIterations}). Terminated to prevent infinite loop.";

            await RecordStepAsync<object?>(state, AgentStepType.FAIL, "Loop Limit Exceeded",
                state.ErrorMessage, null, () => Task.FromResult<object?>(null), onStep);
        }

        return state;
    }

    private class StepExecutionHolder<T>
    {
        public AgentStep Step { get; set; } = null!;
        public T? ResultObject { get; set; }
    }

    private async Task<StepExecutionHolder<T>> RecordStepAsync<T>(
        AgentState state,
        AgentStepType type,
        string title,
        string description,
        string? inputPayload,
        Func<Task<T>> action,
        Action<AgentStep>? onStep)
    {
        var sw = Stopwatch.StartNew();
        var step = new AgentStep
        {
            RunId = state.RunId,
            StepIndex = state.Steps.Count + 1,
            Type = type,
            Title = title,
            Description = description,
            Timestamp = DateTime.UtcNow,
            InputPayload = inputPayload,
            Status = "Running"
        };

        T? result = default;
        try
        {
            result = await action();
            step.Status = (type == AgentStepType.FAIL) ? "Failed" : "Success";
            if (result is ToolResult tr)
            {
                step.OutputPayload = tr.IsSuccess ? tr.OutputJson : tr.ErrorMessage;
                step.Status = tr.IsSuccess ? "Success" : tr.Status.ToString();
            }
            else if (result is ModelCompletionResult mcr)
            {
                step.OutputPayload = mcr.HasToolCalls
                    ? $"Requested {mcr.ToolCalls.Count} tool(s): {string.Join(", ", mcr.ToolCalls.Select(t => t.ToolName))}"
                    : mcr.Content;
            }
            else if (result != null)
            {
                step.OutputPayload = result.ToString();
            }
        }
        catch (Exception ex)
        {
            step.Status = "Failed";
            step.ErrorDetails = ex.Message;
            step.OutputPayload = $"Error: {ex.Message}";
        }
        finally
        {
            sw.Stop();
            step.DurationMs = sw.Elapsed.TotalMilliseconds;
            state.Steps.Add(step);
            _tracer.RecordEvent(state.RunId, state.ConversationId, $"STEP_{type}", step.DurationMs, step.Description, step.Status);
            onStep?.Invoke(step);
        }

        return new StepExecutionHolder<T> { Step = step, ResultObject = result };
    }
}

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        Func<AgentStep, Task>? onStep = null,
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
                state.UserPrompt,
                () => _modelProvider.GenerateAsync(state.Messages, state.AvailableTools, cancellationToken),
                onStep, cancellationToken);

            var modelCompletion = planStep.ResultObject;
            if (modelCompletion != null && modelCompletion.IsFallback)
            {
                planStep.Step.Description += $" [AI Model Fallback: {modelCompletion.FallbackReason}]";
            }

            if (modelCompletion == null)
            {
                state.Status = RunStatus.Failed;
                state.ErrorMessage = planStep.Step.ErrorDetails is { } err
                    ? $"Model provider failed: {err}"
                    : "Model completion returned null.";
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
                    state.FinalAnswer, () => Task.FromResult<object?>(null), onStep, cancellationToken);

                break;
            }

            // Record the assistant turn that requested the tools, so the model
            // sees its own tool calls on the next iteration (tool-calling protocol).
            state.Messages.Add(new AgentMessage
            {
                ConversationId = state.ConversationId,
                Role = MessageRole.MODEL_OUTPUT,
                Content = modelCompletion.Content,
                ToolCalls = modelCompletion.ToolCalls.ToList()
            });

            // Execute every requested tool call in order.
            ToolResult? blockedResult = null;
            ToolCall? blockedCall = null;

            foreach (var toolCall in modelCompletion.ToolCalls)
            {
                var toolResult = await ExecuteToolCallStepsAsync(state, toolCall, onStep, cancellationToken);

                if (toolResult.Status is ToolExecutionStatus.Blocked or ToolExecutionStatus.AwaitingApproval)
                {
                    blockedResult = toolResult;
                    blockedCall = toolCall;
                    break;
                }
            }

            // If a tool was blocked or requires approval, generate a safe explanatory finish
            if (blockedResult != null && blockedCall != null)
            {
                var guardPrefix = blockedResult.Status == ToolExecutionStatus.AwaitingApproval
                    ? "Harness Approval Required"
                    : "Harness Security Guardrail";

                state.FinalAnswer = $"{guardPrefix}: Action '{blockedCall.ToolName}' status: {blockedResult.Status}. Reason: {blockedResult.ErrorMessage}";
                state.Status = RunStatus.Completed;

                await RecordStepAsync<object?>(state, AgentStepType.COMPLETE, "Safely Guarded Completion",
                    "Harness terminated run safely after enforcing security policy restriction.",
                    state.FinalAnswer, () => Task.FromResult<object?>(null), onStep, cancellationToken);

                break;
            }
        }

        // Check if loop exceeded max iterations without finishing
        if (state.CurrentIteration >= state.MaxIterations && state.Status == RunStatus.Running)
        {
            state.Status = RunStatus.Failed;
            state.ErrorMessage = $"Agent exceeded maximum loop iterations limit ({state.MaxIterations}). Terminated to prevent infinite loop.";

            await RecordStepAsync<object?>(state, AgentStepType.FAIL, "Loop Limit Exceeded",
                state.ErrorMessage, null, () => Task.FromResult<object?>(null), onStep, cancellationToken);
        }

        return state;
    }

    /// <summary>
    /// Runs the ACT -> OBSERVE -> EVALUATE nodes for a single tool call and appends
    /// the tool result to working memory. Never returns null.
    /// </summary>
    private async Task<ToolResult> ExecuteToolCallStepsAsync(
        AgentState state,
        ToolCall toolCall,
        Func<AgentStep, Task>? onStep,
        CancellationToken cancellationToken)
    {
        state.PendingToolCall = toolCall;

        // ==========================================
        // 2. NODE: ACT
        // ==========================================
        await RecordStepAsync(state, AgentStepType.ACT, $"Request Tool: {toolCall.ToolName}",
            $"Agent selected tool '{toolCall.ToolName}' with arguments: {toolCall.ArgumentsJson}",
            toolCall.ArgumentsJson,
            () => Task.FromResult(toolCall), onStep, cancellationToken);

        // ==========================================
        // 3. NODE: OBSERVE
        // ==========================================
        var observeStep = await RecordStepAsync(state, AgentStepType.OBSERVE, $"Execute Tool: {toolCall.ToolName}",
            "Dispatched tool call to Harness Policy Engine & Tool Executor.",
            toolCall.ArgumentsJson,
            () => _toolExecutor.ExecuteToolCallAsync(toolCall, state.RunId, cancellationToken),
            onStep, cancellationToken);

        // Guarantee a structured result even if the executor threw.
        var toolResult = observeStep.ResultObject ?? new ToolResult
        {
            ToolCallId = toolCall.Id,
            ToolName = toolCall.ToolName,
            IsSuccess = false,
            Status = ToolExecutionStatus.Failed,
            ErrorMessage = observeStep.Step.ErrorDetails ?? "Tool executor returned no result."
        };
        state.LastToolResult = toolResult;

        // Update conversation working memory with tool output
        // Sanitize indirect prompt injection patterns in tool outputs before feeding back to model context
        var toolMessageContent = toolResult.IsSuccess
            ? SanitizeToolOutput(toolResult.OutputJson)
            : $"Tool execution failed or was blocked: {toolResult.ErrorMessage}";

        state.Messages.Add(new AgentMessage
        {
            ConversationId = state.ConversationId,
            Role = MessageRole.TOOL,
            Content = toolMessageContent,
            Metadata = new Dictionary<string, string>
            {
                ["ToolName"] = toolCall.ToolName,
                ["ToolCallId"] = toolCall.Id,
                ["Status"] = toolResult.Status.ToString()
            }
        });

        // ==========================================
        // 4. NODE: EVALUATE
        // ==========================================
        await RecordStepAsync(state, AgentStepType.EVALUATE, "Evaluate Tool Result",
            $"Evaluated result from {toolCall.ToolName}. Success: {toolResult.IsSuccess}, Status: {toolResult.Status}",
            toolMessageContent,
            () => Task.FromResult(toolResult), onStep, cancellationToken);

        return toolResult;
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
        Func<AgentStep, Task>? onStep,
        CancellationToken cancellationToken)
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
                step.OutputPayload = result is ToolCall tc
                    ? JsonSerializer.Serialize(new { tc.Id, tc.ToolName, tc.ArgumentsJson })
                    : result.ToString();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller cancelled the run: record and propagate instead of swallowing.
            step.Status = "Cancelled";
            step.ErrorDetails = "Run was cancelled.";
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Step {StepType} failed for Run {RunId}", type, state.RunId);
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
        }

        // Notify outside the try/finally so a failing observer (e.g. a disconnected
        // SSE client) surfaces to the caller rather than being recorded as a step error.
        if (onStep != null)
        {
            await onStep(step);
        }

        return new StepExecutionHolder<T> { Step = step, ResultObject = result };
    }

    private static readonly Regex IndirectInjectionRegex = new(
        @"(IGNORE\s+ALL\s+PREVIOUS\s+INSTRUCTIONS|SYSTEM\s+PROMPT\s+OVERRIDE|<script[\s\S]*?>[\s\S]*?<\/script>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string SanitizeToolOutput(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput)) return rawOutput;
        if (IndirectInjectionRegex.IsMatch(rawOutput))
        {
            return IndirectInjectionRegex.Replace(rawOutput, "[REDACTED_SECURITY_RISK]");
        }
        return rawOutput;
    }
}

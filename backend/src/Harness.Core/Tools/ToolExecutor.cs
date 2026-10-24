using System.Diagnostics;
using Harness.Core.Interfaces;
using Harness.Models;
using Microsoft.Extensions.Logging;

namespace Harness.Core.Tools;

public class ToolExecutorOptions
{
    public int ExecutionTimeoutSeconds { get; set; } = 30;
}

public class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IPolicyEngine _policyEngine;
    private readonly IInputValidator _inputValidator;
    private readonly IOutputValidator _outputValidator;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IExecutionTracer _tracer;
    private readonly ILogger<ToolExecutor> _logger;
    private readonly IToolExecutionStore? _executionStore;
    private readonly ToolExecutorOptions _options;

    public ToolExecutor(
        IToolRegistry toolRegistry,
        IPolicyEngine policyEngine,
        IOutputValidator outputValidator,
        IRetryPolicy retryPolicy,
        IExecutionTracer tracer,
        ILogger<ToolExecutor> logger,
        IInputValidator? inputValidator = null,
        IToolExecutionStore? executionStore = null,
        ToolExecutorOptions? options = null)
    {
        _toolRegistry = toolRegistry;
        _policyEngine = policyEngine;
        _inputValidator = inputValidator ?? new Validation.InputValidator();
        _outputValidator = outputValidator;
        _retryPolicy = retryPolicy;
        _tracer = tracer;
        _logger = logger;
        _executionStore = executionStore;
        _options = options ?? new ToolExecutorOptions();
    }

    public async Task<ToolResult> ExecuteToolCallAsync(ToolCall call, string runId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        var sw = Stopwatch.StartNew();

        _tracer.RecordEvent(runId, "", "TOOL_CALL_RECEIVED", 0, $"Tool requested: {call.ToolName}");
        _logger.LogInformation("Processing tool call {ToolName} for run {RunId}", call.ToolName, runId);

        // 1. Tool Registry Check
        var tool = _toolRegistry.GetTool(call.ToolName);
        if (tool == null)
        {
            sw.Stop();
            var notFoundMsg = $"Tool '{call.ToolName}' is not registered in the harness tool registry.";
            _tracer.RecordEvent(runId, "", "TOOL_NOT_FOUND", sw.Elapsed.TotalMilliseconds, notFoundMsg, "ERROR");
            return new ToolResult
            {
                ToolCallId = call.Id,
                ToolName = call.ToolName,
                IsSuccess = false,
                ErrorMessage = notFoundMsg,
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
                Status = ToolExecutionStatus.Failed
            };
        }

        // 2. Policy Engine Evaluation
        var policyDecision = await _policyEngine.EvaluatePolicyAsync(tool, call.ArgumentsJson, cancellationToken);
        _tracer.RecordEvent(runId, "", "POLICY_EVALUATED", 0,
            $"Tool: {tool.Name}, Risk: {policyDecision.RiskLevel}, Decision: {policyDecision.Decision}, Reason: {policyDecision.Reason}");

        if (policyDecision.Decision != PolicyDecisionType.ALLOWED)
        {
            sw.Stop();
            _logger.LogWarning("Tool {ToolName} prevented by policy: {Reason}", tool.Name, policyDecision.Reason);
            var isAwaitingApproval = policyDecision.Decision == PolicyDecisionType.REQUIRES_APPROVAL;
            return new ToolResult
            {
                ToolCallId = call.Id,
                ToolName = tool.Name,
                IsSuccess = false,
                ErrorMessage = isAwaitingApproval
                    ? $"Action Paused: {policyDecision.Reason}"
                    : $"Security Policy Violation: {policyDecision.Reason}",
                RiskLevel = tool.RiskLevel,
                Status = isAwaitingApproval ? ToolExecutionStatus.AwaitingApproval : ToolExecutionStatus.Blocked,
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds
            };
        }

        // 3. Input Arguments Schema Validation
        var inputValidation = _inputValidator.Validate(tool, call.ArgumentsJson);
        if (!inputValidation.IsValid)
        {
            sw.Stop();
            _logger.LogWarning("Tool {ToolName} input validation failed: {Msg}", tool.Name, inputValidation.Message);
            _tracer.RecordEvent(runId, "", "INPUT_VALIDATION_FAILED", sw.Elapsed.TotalMilliseconds, inputValidation.Message, "WARN");
            return new ToolResult
            {
                ToolCallId = call.Id,
                ToolName = tool.Name,
                IsSuccess = false,
                ErrorMessage = $"Input Validation Failure: {inputValidation.Message}",
                RiskLevel = tool.RiskLevel,
                Status = ToolExecutionStatus.Failed,
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds
            };
        }

        // 3. Execution with Timeout & Retry Policy
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.ExecutionTimeoutSeconds));

        ToolResult toolResult;
        try
        {
            toolResult = await _retryPolicy.ExecuteWithRetryAsync(
                async attempt =>
                {
                    if (attempt > 1)
                    {
                        _tracer.RecordEvent(runId, "", "TOOL_RETRY_ATTEMPT", 0, $"Retry attempt {attempt} for tool {tool.Name}", "WARN");
                    }
                    _tracer.RecordEvent(runId, "", "TOOL_EXECUTION_ATTEMPT", 0, $"Attempt {attempt} executing {tool.Name}");
                    return await tool.ExecuteAsync(call.ArgumentsJson, cts.Token);
                },
                result =>
                {
                    // Decide if retryable based on OutputValidator
                    if (result.IsSuccess) return false;
                    var validation = _outputValidator.Validate(tool, result);
                    return validation.Outcome == ValidationOutcome.Retryable;
                },
                maxRetries: 2,
                initialDelay: TimeSpan.FromMilliseconds(150),
                cancellationToken: cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own timeout fired (caller did not cancel) -> report a structured failure.
            toolResult = CreateFailure(call, tool,
                $"Timeout: tool '{tool.Name}' exceeded execution limit of {_options.ExecutionTimeoutSeconds}s.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Tool {ToolName} threw an unhandled exception for run {RunId}", tool.Name, runId);
            toolResult = CreateFailure(call, tool, $"Tool '{tool.Name}' threw an exception: {ex.Message}");
        }

        toolResult.ToolCallId = call.Id;
        toolResult.ToolName = tool.Name;
        toolResult.RiskLevel = tool.RiskLevel;

        // 4. Output Validation
        var validationResult = _outputValidator.Validate(tool, toolResult);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Tool {ToolName} output validation failed: {Msg}", tool.Name, validationResult.Message);
            _tracer.RecordEvent(runId, "", "OUTPUT_VALIDATION_FAILED", 0, validationResult.Message, "WARN");

            if (validationResult.Outcome == ValidationOutcome.Fatal || validationResult.Outcome == ValidationOutcome.Invalid)
            {
                toolResult.IsSuccess = false;
                toolResult.Status = ToolExecutionStatus.Failed;
                toolResult.ErrorMessage = $"Validation Failure ({validationResult.Outcome}): {validationResult.Message}";
            }
        }
        else
        {
            _tracer.RecordEvent(runId, "", "OUTPUT_VALIDATION_SUCCESS", 0, "Tool output conforms to schema");
        }

        sw.Stop();
        toolResult.ExecutionDurationMs = sw.Elapsed.TotalMilliseconds;
        _tracer.RecordEvent(runId, "", "TOOL_EXECUTION_COMPLETED", sw.Elapsed.TotalMilliseconds,
            $"Finished {tool.Name}. Success: {toolResult.IsSuccess}");

        if (_executionStore != null)
        {
            try
            {
                await _executionStore.RecordExecutionAsync(new ToolExecutionRecord
                {
                    RunId = runId,
                    ToolName = tool.Name,
                    RiskLevel = tool.RiskLevel,
                    ArgumentsJson = call.ArgumentsJson,
                    OutputJson = toolResult.OutputJson,
                    IsSuccess = toolResult.IsSuccess,
                    DurationMs = toolResult.ExecutionDurationMs,
                    ErrorMessage = toolResult.ErrorMessage,
                    PolicyDecision = policyDecision.Decision
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist ToolExecutionRecord for {ToolName}", tool.Name);
            }
        }

        return toolResult;
    }

    private static ToolResult CreateFailure(ToolCall call, ITool tool, string message) => new()
    {
        ToolCallId = call.Id,
        ToolName = tool.Name,
        IsSuccess = false,
        ErrorMessage = message,
        RiskLevel = tool.RiskLevel,
        Status = ToolExecutionStatus.Failed
    };
}

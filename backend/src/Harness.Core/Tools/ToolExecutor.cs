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
    private readonly IOutputValidator _outputValidator;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IExecutionTracer _tracer;
    private readonly ILogger<ToolExecutor> _logger;
    private readonly ToolExecutorOptions _options;

    public ToolExecutor(
        IToolRegistry toolRegistry,
        IPolicyEngine policyEngine,
        IOutputValidator outputValidator,
        IRetryPolicy retryPolicy,
        IExecutionTracer tracer,
        ILogger<ToolExecutor> logger,
        ToolExecutorOptions? options = null)
    {
        _toolRegistry = toolRegistry;
        _policyEngine = policyEngine;
        _outputValidator = outputValidator;
        _retryPolicy = retryPolicy;
        _tracer = tracer;
        _logger = logger;
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
            _logger.LogWarning("Tool {ToolName} blocked by policy: {Reason}", tool.Name, policyDecision.Reason);
            return new ToolResult
            {
                ToolCallId = call.Id,
                ToolName = tool.Name,
                IsSuccess = false,
                ErrorMessage = $"Security Policy Violation: {policyDecision.Reason}",
                RiskLevel = tool.RiskLevel,
                Status = ToolExecutionStatus.Blocked,
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds
            };
        }

        // 3. Execution with Timeout & Retry Policy
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.ExecutionTimeoutSeconds));

        ToolResult toolResult = await _retryPolicy.ExecuteWithRetryAsync(
            async attempt =>
            {
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

        toolResult.ToolCallId = call.Id;
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

        return toolResult;
    }
}

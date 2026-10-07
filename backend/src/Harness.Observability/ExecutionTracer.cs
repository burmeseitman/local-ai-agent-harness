using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Harness.Core.Interfaces;
using Harness.Models;
using Microsoft.Extensions.Logging;

namespace Harness.Observability;

public class ExecutionTracer : IExecutionTracer
{
    private static readonly ActivitySource ActivitySource = new("SecureAiHarness.Engine", "1.0.0");
    private static readonly Meter HarnessMeter = new("SecureAiHarness.Metrics", "1.0.0");

    private static readonly Counter<long> RunsCounter = HarnessMeter.CreateCounter<long>("harness.runs.total", "Total agent runs");
    private static readonly Counter<long> ToolCallsCounter = HarnessMeter.CreateCounter<long>("harness.tool_calls.total", "Total tool calls");
    private static readonly Counter<long> RetriesCounter = HarnessMeter.CreateCounter<long>("harness.retries.total", "Total retried attempts");
    private static readonly Histogram<double> LatencyHistogram = HarnessMeter.CreateHistogram<double>("harness.run.duration_ms", "ms", "Run duration");

    private readonly ConcurrentBag<ExecutionTrace> _traces = new();
    private readonly ILogger<ExecutionTracer> _logger;

    public ExecutionTracer(ILogger<ExecutionTracer> logger)
    {
        _logger = logger;
    }

    public void RecordEvent(
        string runId,
        string conversationId,
        string operation,
        double latencyMs,
        string? details = null,
        string status = "OK")
    {
        var trace = new ExecutionTrace
        {
            TraceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            RunId = runId,
            ConversationId = conversationId,
            Timestamp = DateTime.UtcNow,
            Operation = operation,
            LatencyMs = latencyMs,
            Details = details,
            Status = status
        };

        _traces.Add(trace);

        if (status == "ERROR")
        {
            _logger.LogError("[TRACE {TraceId}] Run: {RunId} | Op: {Operation} | Latency: {LatencyMs}ms | Status: {Status} | Details: {Details}",
                trace.TraceId, runId, operation, latencyMs, status, details);
        }
        else if (status == "WARN")
        {
            _logger.LogWarning("[TRACE {TraceId}] Run: {RunId} | Op: {Operation} | Latency: {LatencyMs}ms | Status: {Status} | Details: {Details}",
                trace.TraceId, runId, operation, latencyMs, status, details);
        }
        else
        {
            _logger.LogInformation("[TRACE {TraceId}] Run: {RunId} | Op: {Operation} | Latency: {LatencyMs}ms | Status: {Status} | Details: {Details}",
                trace.TraceId, runId, operation, latencyMs, status, details);
        }

        // Metrics increment
        if (operation == "RUN_INITIATED") RunsCounter.Add(1);
        if (operation == "TOOL_EXECUTION_COMPLETED") ToolCallsCounter.Add(1);
        if (operation.Contains("RETRY")) RetriesCounter.Add(1);
        if (operation == "RUN_FINISHED") LatencyHistogram.Record(latencyMs);
    }

    public List<ExecutionTrace> GetTracesForRun(string runId)
    {
        return _traces
            .Where(t => t.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Timestamp)
            .ToList();
    }

    public List<ExecutionTrace> GetAllTraces(int limit = 100)
    {
        return _traces
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .ToList();
    }
}

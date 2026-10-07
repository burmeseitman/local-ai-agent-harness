using System.Diagnostics;
using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Tools;

/// <summary>
/// Tool designed to simulate transient network/API hiccups for testing retry policies.
/// Fails on the first invocation, then succeeds on retry.
/// </summary>
public class FlakySimulatorTool : ITool
{
    private static int _attemptCounter = 0;

    public string Name => "flaky_tool_simulator";
    public string Description => "Simulates a transient flaky network service that fails initially then succeeds upon retry.";
    public ToolRiskLevel RiskLevel => ToolRiskLevel.LOW;

    public JsonDocument InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "retryCount": { "type": "number", "description": "Number of simulated retries expected" }
        }
    }
    """);

    public JsonDocument OutputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["status", "attempts", "message"],
        "properties": {
            "status": { "type": "string" },
            "attempts": { "type": "number" },
            "message": { "type": "string" }
        }
    }
    """);

    public static void ResetCounter() => _attemptCounter = 0;

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _attemptCounter++;

        if (_attemptCounter == 1)
        {
            sw.Stop();
            return Task.FromResult(new ToolResult
            {
                ToolName = Name,
                IsSuccess = false,
                ErrorMessage = "Transient upstream socket connection timeout (504 Gateway Timeout). Retryable.",
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
                RiskLevel = RiskLevel,
                Status = ToolExecutionStatus.Failed
            });
        }

        sw.Stop();
        var payload = new
        {
            status = "RECOVERED",
            attempts = _attemptCounter,
            message = "Service recovered successfully on subsequent retry attempt."
        };

        return Task.FromResult(new ToolResult
        {
            ToolName = Name,
            IsSuccess = true,
            OutputJson = JsonSerializer.Serialize(payload),
            ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
            RiskLevel = RiskLevel,
            Status = ToolExecutionStatus.Success
        });
    }
}

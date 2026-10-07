using System.Diagnostics;
using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Tools;

public class GetServerStatusTool : ITool
{
    public string Name => "get_server_status";
    public string Description => "Retrieves current server health, CPU usage, memory usage, uptime, and system status.";
    public ToolRiskLevel RiskLevel => ToolRiskLevel.LOW;

    public JsonDocument InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "detailed": { "type": "boolean", "description": "Include detailed core diagnostics" }
        }
    }
    """);

    public JsonDocument OutputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["status", "cpuUsagePercent", "memoryUsagePercent", "uptimeSeconds", "timestamp"],
        "properties": {
            "status": { "type": "string" },
            "cpuUsagePercent": { "type": "number" },
            "memoryUsagePercent": { "type": "number" },
            "uptimeSeconds": { "type": "number" },
            "timestamp": { "type": "string" }
        }
    }
    """);

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var random = new Random();

        var statusData = new
        {
            status = "HEALTHY",
            cpuUsagePercent = Math.Round(22.5 + random.NextDouble() * 15, 1),
            memoryUsagePercent = Math.Round(58.0 + random.NextDouble() * 10, 1),
            uptimeSeconds = 345600,
            activeThreads = 48,
            diskUsagePercent = 42.1,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        sw.Stop();
        return Task.FromResult(new ToolResult
        {
            ToolName = Name,
            IsSuccess = true,
            OutputJson = JsonSerializer.Serialize(statusData),
            ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
            RiskLevel = RiskLevel,
            Status = ToolExecutionStatus.Success
        });
    }
}

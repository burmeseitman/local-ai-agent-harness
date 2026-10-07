using System.Diagnostics;
using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Tools;

public class GetSystemLogsTool : ITool
{
    public string Name => "get_system_logs";
    public string Description => "Searches recent application and system log entries filtered by log level and keyword.";
    public ToolRiskLevel RiskLevel => ToolRiskLevel.MEDIUM;

    public JsonDocument InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "level": { "type": "string", "enum": ["info", "warn", "error"], "description": "Minimum log severity level" },
            "limit": { "type": "integer", "description": "Max entries to retrieve (max 50)" },
            "filter": { "type": "string", "description": "Keyword filter" }
        }
    }
    """);

    public JsonDocument OutputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["count", "logs"],
        "properties": {
            "count": { "type": "number" },
            "logs": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "timestamp": { "type": "string" },
                        "level": { "type": "string" },
                        "source": { "type": "string" },
                        "message": { "type": "string" }
                    }
                }
            }
        }
    }
    """);

    private static readonly List<(DateTime Timestamp, string Level, string Source, string Message)> SampleLogs = new()
    {
        (DateTime.UtcNow.AddMinutes(-12), "ERROR", "DbConnectionPool", "Timeout waiting for connection after 15000ms. Active: 100/100."),
        (DateTime.UtcNow.AddMinutes(-10), "WARN", "MemoryWatchdog", "Memory usage climbed to 81.4% threshold."),
        (DateTime.UtcNow.AddMinutes(-8), "INFO", "HarnessRunner", "Agent cycle completed successfully in 320ms."),
        (DateTime.UtcNow.AddMinutes(-5), "WARN", "RateLimiter", "Client [10.0.4.12] approached 80% of token bucket."),
        (DateTime.UtcNow.AddMinutes(-2), "INFO", "HealthMonitor", "Health check probe OK (latency: 4ms).")
    };

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string level = "info";
        int limit = 10;
        string filter = "";

        try
        {
            if (!string.IsNullOrWhiteSpace(argumentsJson))
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("level", out var lProp)) level = lProp.GetString()?.ToLowerInvariant() ?? "info";
                if (root.TryGetProperty("limit", out var limProp) && limProp.TryGetInt32(out var lVal)) limit = Math.Clamp(lVal, 1, 50);
                if (root.TryGetProperty("filter", out var fProp)) filter = fProp.GetString()?.ToLowerInvariant() ?? "";
            }
        }
        catch
        {
            // use defaults
        }

        var filtered = SampleLogs.Where(l =>
        {
            if (level == "error" && l.Level != "ERROR") return false;
            if (level == "warn" && l.Level != "WARN" && l.Level != "ERROR") return false;
            if (!string.IsNullOrEmpty(filter) && !l.Message.ToLowerInvariant().Contains(filter) && !l.Source.ToLowerInvariant().Contains(filter)) return false;
            return true;
        }).Take(limit).Select(l => new
        {
            timestamp = l.Timestamp.ToString("o"),
            level = l.Level,
            source = l.Source,
            message = l.Message
        }).ToList();

        sw.Stop();
        var payload = new
        {
            count = filtered.Count,
            logs = filtered
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

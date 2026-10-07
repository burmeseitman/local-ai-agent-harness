using System.Diagnostics;
using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Tools;

public class GetIncidentHistoryTool : ITool
{
    public string Name => "get_incident_history";
    public string Description => "Retrieves historical system incident reports, root cause analyses, and mitigation records.";
    public ToolRiskLevel RiskLevel => ToolRiskLevel.LOW;

    public JsonDocument InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "severity": { "type": "string", "enum": ["low", "medium", "high", "critical"], "description": "Filter by incident severity" },
            "system": { "type": "string", "description": "Filter by affected system" }
        }
    }
    """);

    public JsonDocument OutputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["totalIncidents", "incidents"],
        "properties": {
            "totalIncidents": { "type": "number" },
            "incidents": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "incidentId": { "type": "string" },
                        "severity": { "type": "string" },
                        "title": { "type": "string" },
                        "rootCause": { "type": "string" },
                        "resolution": { "type": "string" },
                        "resolvedAt": { "type": "string" }
                    }
                }
            }
        }
    }
    """);

    private static readonly List<(string Id, string Severity, string Title, string RootCause, string Resolution, string ResolvedAt)> Incidents = new()
    {
        ("INC-801", "high", "PostgreSQL Connection Starvation",
         "A burst of 1200 concurrent agent worker tasks exhausted the connection pool, resulting in HTTP 500 responses.",
         "Scaled connection pool size to 250 and implemented circuit breaker around batch tasks.",
         "2026-09-18T14:30:00Z"),

        ("INC-784", "medium", "Prompt Injection Attempt in User Input",
         "Adversarial payload tried to bypass harness policies with jailbreak instructions.",
         "Blocked by Policy Engine input validation filter; logged to audit security trail.",
         "2026-09-14T09:12:00Z"),

        ("INC-762", "low", "Ollama LLM Latency Spike",
         "Model inference queue stalled due to concurrent memory load on local GPU.",
         "Optimized prompt context window and enforced 30s timeout with fallback.",
         "2026-09-02T18:45:00Z")
    };

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string severity = "";
        string system = "";

        try
        {
            if (!string.IsNullOrWhiteSpace(argumentsJson))
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("severity", out var sProp)) severity = sProp.GetString()?.ToLowerInvariant() ?? "";
                if (root.TryGetProperty("system", out var sysProp)) system = sysProp.GetString()?.ToLowerInvariant() ?? "";
            }
        }
        catch
        {
            // default
        }

        var matches = Incidents.Where(i =>
            (string.IsNullOrEmpty(severity) || i.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(system) || i.Title.ToLowerInvariant().Contains(system) || i.RootCause.ToLowerInvariant().Contains(system))
        ).Select(i => new
        {
            incidentId = i.Id,
            severity = i.Severity,
            title = i.Title,
            rootCause = i.RootCause,
            resolution = i.Resolution,
            resolvedAt = i.ResolvedAt
        }).ToList();

        sw.Stop();
        var payload = new
        {
            totalIncidents = matches.Count,
            incidents = matches
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

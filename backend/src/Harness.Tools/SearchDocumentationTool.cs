using System.Diagnostics;
using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Tools;

public class SearchDocumentationTool : ITool
{
    public string Name => "search_documentation";
    public string Description => "Searches local internal technical documentation and runbooks for relevant articles and guides.";
    public ToolRiskLevel RiskLevel => ToolRiskLevel.LOW;

    public JsonDocument InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["query"],
        "properties": {
            "query": { "type": "string", "description": "Search keyword or topic" },
            "category": { "type": "string", "description": "Optional category filter (e.g., database, networking, security)" }
        }
    }
    """);

    public JsonDocument OutputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["totalFound", "results"],
        "properties": {
            "totalFound": { "type": "number" },
            "results": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "id": { "type": "string" },
                        "title": { "type": "string" },
                        "content": { "type": "string" },
                        "category": { "type": "string" }
                    }
                }
            }
        }
    }
    """);

    private static readonly List<(string Id, string Title, string Category, string Content)> Docs = new()
    {
        ("DOC-001", "Resolving Database Connection Failures", "database",
         "When encountering connection pool exhaustion or socket timeouts: 1. Verify connection string pool size (default Max Pool Size=100). 2. Inspect active queries using pg_stat_activity. 3. Check firewall rules between app tier and port 5432. 4. Verify SSL certificate expiration."),

        ("DOC-002", "Agent Harness Sandbox Configuration", "security",
         "All tools must execute through the isolated IToolExecutor. Arbitrary shell, raw file deletion, and direct SQL execution are prohibited by default security policy. Tools must provide deterministic JSON schemas."),

        ("DOC-003", "Application High Availability and Failover", "infrastructure",
         "Services deploy in active-passive clustering with automated health check probes at /api/health. If probes fail 3 consecutive times within 15 seconds, DNS switches traffic to secondary replica."),

        ("DOC-004", "Handling Transient HTTP Timeouts", "networking",
         "Transient timeouts should use exponential backoff: initial delay 200ms, factor 2.0, max retries 2. Avoid retrying 4xx client errors or policy blocks.")
    };

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string query = "";

        try
        {
            if (!string.IsNullOrWhiteSpace(argumentsJson))
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                if (doc.RootElement.TryGetProperty("query", out var q))
                {
                    query = q.GetString() ?? "";
                }
            }
        }
        catch
        {
            // fallback empty
        }

        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = Docs.Where(d =>
            terms.Length == 0 ||
            terms.Any(t => d.Title.ToLowerInvariant().Contains(t) || d.Content.ToLowerInvariant().Contains(t) || d.Category.Contains(t))
        ).Select(d => new
        {
            id = d.Id,
            title = d.Title,
            category = d.Category,
            content = d.Content
        }).ToList();

        sw.Stop();
        var resultObj = new
        {
            totalFound = matches.Count,
            results = matches
        };

        return Task.FromResult(new ToolResult
        {
            ToolName = Name,
            IsSuccess = true,
            OutputJson = JsonSerializer.Serialize(resultObj),
            ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
            RiskLevel = RiskLevel,
            Status = ToolExecutionStatus.Success
        });
    }
}

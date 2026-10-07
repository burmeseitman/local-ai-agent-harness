using System.Diagnostics;
using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Tools;

/// <summary>
/// A simulated high-risk tool created specifically to verify that Policy Engine
/// blocks or requires human approval for dangerous operations.
/// </summary>
public class DeleteClusterTool : ITool
{
    public string Name => "delete_server_cluster";
    public string Description => "Destructive operation to shut down and delete an entire production server cluster.";
    public ToolRiskLevel RiskLevel => ToolRiskLevel.CRITICAL;

    public JsonDocument InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["target"],
        "properties": {
            "target": { "type": "string", "description": "Cluster identifier to destroy" }
        }
    }
    """);

    public JsonDocument OutputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "deleted": { "type": "boolean" },
            "target": { "type": "string" }
        }
    }
    """);

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        // Even if reached, it never performs actual deletion
        sw.Stop();
        return Task.FromResult(new ToolResult
        {
            ToolName = Name,
            IsSuccess = false,
            ErrorMessage = "Security Violation: Destructive cluster deletion is strictly prohibited in local demo.",
            ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
            RiskLevel = RiskLevel,
            Status = ToolExecutionStatus.Blocked
        });
    }
}

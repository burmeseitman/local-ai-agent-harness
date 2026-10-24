using Harness.Core.Interfaces;
using Harness.Infrastructure.Data;
using Harness.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Harness.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToolsController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;
    private readonly AppDbContext _db;

    public ToolsController(
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        AppDbContext db)
    {
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetToolsAsync(CancellationToken cancellationToken)
    {
        var tools = _toolRegistry.GetAllTools();
        var executionCounts = await _db.ToolExecutions
            .GroupBy(e => e.ToolName)
            .Select(g => new
            {
                ToolName = g.Key,
                Count = g.Count(),
                AvgLatency = g.Average(x => x.DurationMs)
            })
            .ToDictionaryAsync(x => x.ToolName, cancellationToken);

        var result = tools.Select(t =>
        {
            executionCounts.TryGetValue(t.Name, out var stats);
            return new
            {
                name = t.Name,
                description = t.Description,
                riskLevel = t.RiskLevel.ToString(),
                inputSchema = t.InputSchema.RootElement,
                outputSchema = t.OutputSchema.RootElement,
                executionCount = stats?.Count ?? 0,
                averageLatencyMs = Math.Round(stats?.AvgLatency ?? 0, 1),
                isAllowed = t.RiskLevel != ToolRiskLevel.CRITICAL && t.RiskLevel != ToolRiskLevel.HIGH
            };
        });

        return Ok(result);
    }

    [HttpGet("{toolName}")]
    public async Task<ActionResult<object>> GetToolByNameAsync(string toolName, CancellationToken cancellationToken)
    {
        var tool = _toolRegistry.GetTool(toolName);
        if (tool == null)
        {
            return NotFound(new { error = $"Tool '{toolName}' not found." });
        }

        var executions = await _db.ToolExecutions
            .Where(e => e.ToolName == toolName)
            .OrderByDescending(e => e.ExecutedAt)
            .Take(15)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            name = tool.Name,
            description = tool.Description,
            riskLevel = tool.RiskLevel.ToString(),
            inputSchema = tool.InputSchema.RootElement,
            outputSchema = tool.OutputSchema.RootElement,
            recentExecutions = executions
        });
    }

    [HttpPost("{toolName}/execute")]
    public async Task<ActionResult<ToolResult>> ExecuteToolAsync(
        string toolName,
        [FromBody] System.Text.Json.JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var tool = _toolRegistry.GetTool(toolName);
        if (tool == null)
        {
            return NotFound(new { error = $"Tool '{toolName}' not found." });
        }

        var toolCall = new ToolCall
        {
            ToolName = toolName,
            ArgumentsJson = arguments.GetRawText()
        };

        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall, "manual-test-run", cancellationToken);

        return Ok(result);
    }
}

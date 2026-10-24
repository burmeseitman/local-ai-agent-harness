using Harness.Core.Interfaces;
using Harness.Infrastructure.Data;
using Harness.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Harness.Api.Controllers;

[ApiController]
[Route("api")]
public class ObservabilityController : ControllerBase
{
    private readonly IExecutionTracer _tracer;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAiModelProvider _aiModelProvider;

    public ObservabilityController(
        IExecutionTracer tracer,
        AppDbContext db,
        IConfiguration config,
        IAiModelProvider aiModelProvider)
    {
        _tracer = tracer;
        _db = db;
        _config = config;
        _aiModelProvider = aiModelProvider;
    }

    [HttpGet("health")]
    public async Task<ActionResult<object>> GetHealthAsync(CancellationToken cancellationToken)
    {
        bool dbHealthy = false;
        try
        {
            dbHealthy = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            dbHealthy = false;
        }

        var overallStatus = dbHealthy ? "HEALTHY" : "DEGRADED";

        return Ok(new
        {
            status = overallStatus,
            timestamp = DateTime.UtcNow,
            service = "Secure Local AI Agent Harness",
            version = "1.0.0",
            guardrailsActive = true,
            components = new
            {
                database = dbHealthy ? "HEALTHY" : "UNHEALTHY",
                modelProvider = _aiModelProvider.ProviderName,
                activeModel = _aiModelProvider.ModelName
            }
        });
    }

    [HttpGet("config")]
    public ActionResult<object> GetConfig()
    {
        return Ok(new
        {
            aiProvider = _aiModelProvider.ProviderName,
            modelName = _aiModelProvider.ModelName,
            ollamaBaseUrl = _config["OLLAMA_BASE_URL"] ?? "http://localhost:11434",
            autoExecuteLowRisk = true,
            autoExecuteMediumRisk = true,
            requireApprovalHighRisk = true,
            blockCriticalRisk = true,
            maxIterations = 10,
            toolTimeoutSeconds = 30,
            maxResultBytes = 65536,
            databaseProvider = _config["DATABASE_PROVIDER"] ?? "sqlite",
            connectionString = _config["CONNECTION_STRING"] ?? "Data Source=secure_harness.db"
        });
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<HarnessMetrics>> GetMetricsAsync(CancellationToken cancellationToken)
    {
        var totalRuns = await _db.AgentRuns.LongCountAsync(cancellationToken);
        var successRuns = await _db.AgentRuns.LongCountAsync(r => r.Status == RunStatus.Completed, cancellationToken);
        var failedRuns = await _db.AgentRuns.LongCountAsync(r => r.Status == RunStatus.Failed, cancellationToken);
        var avgLatency = totalRuns > 0 ? await _db.AgentRuns.AverageAsync(r => r.LatencyMs, cancellationToken) : 0;
        var avgLoops = totalRuns > 0 ? await _db.AgentRuns.AverageAsync(r => r.LoopCount, cancellationToken) : 0;

        var toolExecs = await _db.ToolExecutions.LongCountAsync(cancellationToken);
        var toolFails = await _db.ToolExecutions.LongCountAsync(t => !t.IsSuccess, cancellationToken);
        var realRetries = _tracer.GetRetryCount();

        return Ok(new HarnessMetrics
        {
            TotalRuns = totalRuns,
            SuccessfulRuns = successRuns,
            FailedRuns = failedRuns,
            AverageLatencyMs = Math.Round(avgLatency, 1),
            ToolCalls = toolExecs,
            ToolFailures = toolFails,
            Retries = realRetries,
            AverageLoopCount = Math.Round(avgLoops, 1)
        });
    }

    [HttpGet("traces/{runId}")]
    public ActionResult<List<ExecutionTrace>> GetTracesForRun(string runId)
    {
        var traces = _tracer.GetTracesForRun(runId);
        return Ok(traces);
    }

    [HttpGet("traces")]
    public ActionResult<List<ExecutionTrace>> GetAllTraces([FromQuery] int limit = 100)
    {
        var traces = _tracer.GetAllTraces(limit);
        return Ok(traces);
    }
}

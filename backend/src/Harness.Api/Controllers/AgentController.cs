using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Infrastructure.Data;
using Harness.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Harness.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IHarnessRunner _harnessRunner;
    private readonly AppDbContext _db;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IHarnessRunner harnessRunner,
        AppDbContext db,
        ILogger<AgentController> logger)
    {
        _harnessRunner = harnessRunner;
        _db = db;
        _logger = logger;
    }

    [HttpPost("chat")]
    [HttpPost("run")]
    public async Task<ActionResult<AgentResponse>> RunAsync([FromBody] AgentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt cannot be empty." });
        }

        try
        {
            var response = await _harnessRunner.RunAsync(request, null, cancellationToken);
            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "Request was cancelled by client." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing agent run.");
            return StatusCode(500, new { error = "An internal error occurred while processing the agent request." });
        }
    }

    [HttpGet("stream")]
    public async Task StreamRunAsync([FromQuery] string prompt, [FromQuery] string? conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no"); // disable nginx proxy buffering

        var request = new AgentRequest
        {
            Prompt = prompt,
            ConversationId = conversationId,
            StreamEvents = true
        };

        try
        {
            // Callback is awaited by the AgentLoop, so writes are sequential and
            // exceptions (e.g. client disconnect) propagate here instead of crashing the process.
            var response = await _harnessRunner.RunAsync(
                request,
                step => WriteSseEventAsync(new { type = "step_update", step }, cancellationToken),
                cancellationToken);

            await WriteSseEventAsync(new { type = "run_completed", response }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SSE client disconnected; agent stream cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streamed agent run.");
            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await WriteSseEventAsync(new { type = "run_error", error = "Agent run failed. See server logs for details." }, cancellationToken);
                }
                catch (Exception writeEx)
                {
                    _logger.LogDebug(writeEx, "Failed to write SSE error event (client likely gone).");
                }
            }
        }
    }

    // Match ASP.NET Core MVC output (camelCase) so the frontend can read SSE payloads.
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    private async Task WriteSseEventAsync(object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, SseJsonOptions);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private static AgentRun ToRunEntity(AgentResponse response, string prompt) => new()
    {
        Id = response.RunId,
        ConversationId = response.ConversationId,
        UserPrompt = prompt,
        Status = response.Status,
        StartTime = DateTime.UtcNow.AddMilliseconds(-response.LatencyMs),
        EndTime = DateTime.UtcNow,
        LatencyMs = response.LatencyMs,
        LoopCount = response.LoopCount,
        FinalAnswer = response.FinalAnswer,
        ErrorMessage = response.ErrorMessage,
        Steps = response.Steps
    };

    [HttpGet("runs")]
    public async Task<ActionResult<List<AgentRun>>> GetAllRunsAsync(CancellationToken cancellationToken)
    {
        var runs = await _db.AgentRuns
            .Include(r => r.Steps)
            .OrderByDescending(r => r.StartTime)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(runs);
    }

    [HttpGet("runs/{runId}")]
    public async Task<ActionResult<AgentRun>> GetRunByIdAsync(string runId, CancellationToken cancellationToken)
    {
        var run = await _db.AgentRuns
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        if (run == null)
        {
            return NotFound(new { error = $"Run with ID '{runId}' not found." });
        }

        return Ok(run);
    }
}

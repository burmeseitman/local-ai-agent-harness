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

            // Save run to database
            var runEntity = new AgentRun
            {
                Id = response.RunId,
                ConversationId = response.ConversationId,
                UserPrompt = request.Prompt,
                Status = response.Status,
                StartTime = DateTime.UtcNow.AddMilliseconds(-response.LatencyMs),
                EndTime = DateTime.UtcNow,
                LatencyMs = response.LatencyMs,
                LoopCount = response.LoopCount,
                FinalAnswer = response.FinalAnswer,
                ErrorMessage = response.ErrorMessage,
                Steps = response.Steps
            };

            _db.AgentRuns.Add(runEntity);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "Request was cancelled by client." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing agent run.");
            return StatusCode(500, new { error = ex.Message });
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

        var request = new AgentRequest
        {
            Prompt = prompt,
            ConversationId = conversationId,
            StreamEvents = true
        };

        var response = await _harnessRunner.RunAsync(request, async step =>
        {
            var evtData = JsonSerializer.Serialize(new
            {
                type = "step_update",
                step
            });
            await Response.WriteAsync($"data: {evtData}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }, cancellationToken);

        // Record to DB
        _db.AgentRuns.Add(new AgentRun
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
        });
        await _db.SaveChangesAsync(cancellationToken);

        var finalEvt = JsonSerializer.Serialize(new
        {
            type = "run_completed",
            response
        });
        await Response.WriteAsync($"data: {finalEvt}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

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

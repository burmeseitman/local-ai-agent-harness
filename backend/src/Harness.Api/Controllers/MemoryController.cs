using Harness.Core.Interfaces;
using Harness.Models;
using Microsoft.AspNetCore.Mvc;

namespace Harness.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryService _memoryService;

    public MemoryController(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MemoryItem>>> GetAllAsync([FromQuery] MemoryType? type, CancellationToken cancellationToken)
    {
        var items = await _memoryService.GetAllMemoriesAsync(type, cancellationToken);
        return Ok(items);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<MemoryItem>>> SearchAsync([FromQuery] string q, [FromQuery] int limit = 5, CancellationToken cancellationToken = default)
    {
        var items = await _memoryService.SearchMemoryAsync(q, limit, cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<MemoryItem>> CreateAsync([FromBody] MemoryItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.Content))
        {
            return BadRequest(new { error = "Memory content cannot be empty." });
        }

        var created = await _memoryService.AddMemoryAsync(item, cancellationToken);
        return Created($"/api/memory/{created.Id}", created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var deleted = await _memoryService.DeleteMemoryAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { error = $"Memory item '{id}' not found." });
        }
        return NoContent();
    }
}

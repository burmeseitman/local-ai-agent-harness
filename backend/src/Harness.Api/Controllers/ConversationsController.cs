using Harness.Core.Interfaces;
using Harness.Models;
using Microsoft.AspNetCore.Mvc;

namespace Harness.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationManager _conversationManager;

    public ConversationsController(IConversationManager conversationManager)
    {
        _conversationManager = conversationManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<Conversation>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var convs = await _conversationManager.GetAllConversationsAsync(cancellationToken);
        return Ok(convs);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Conversation>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var conv = await _conversationManager.GetOrCreateConversationAsync(id, cancellationToken);
        var messages = await _conversationManager.GetHistoryAsync(id, 50, cancellationToken);
        conv.Messages = messages;
        return Ok(conv);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> ClearAsync(string id, CancellationToken cancellationToken)
    {
        await _conversationManager.ClearConversationAsync(id, cancellationToken);
        return NoContent();
    }
}

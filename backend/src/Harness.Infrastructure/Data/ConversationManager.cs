using Harness.Core.Interfaces;
using Harness.Models;
using Microsoft.EntityFrameworkCore;

namespace Harness.Infrastructure.Data;

public class ConversationManager : IConversationManager
{
    private readonly AppDbContext _db;

    public ConversationManager(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Conversation> GetOrCreateConversationAsync(string? conversationId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(conversationId))
        {
            var existing = await _db.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

            if (existing != null)
            {
                return existing;
            }
        }

        var conv = new Conversation
        {
            Id = !string.IsNullOrEmpty(conversationId) ? conversationId : Guid.NewGuid().ToString("N"),
            Title = "Agent Session",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync(cancellationToken);
        return conv;
    }

    public async Task<AgentMessage> AddMessageAsync(
        string conversationId,
        MessageRole role,
        string content,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conv == null)
        {
            conv = await GetOrCreateConversationAsync(conversationId, cancellationToken);
        }

        conv.UpdatedAt = DateTime.UtcNow;
        if (conv.Title == "Agent Session" && role == MessageRole.USER)
        {
            conv.Title = content.Length > 35 ? content[..32] + "..." : content;
        }

        var msg = new AgentMessage
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            Metadata = metadata ?? new()
        };

        _db.Messages.Add(msg);
        await _db.SaveChangesAsync(cancellationToken);
        return msg;
    }

    public async Task<List<AgentMessage>> GetHistoryAsync(string conversationId, int limit = 20, CancellationToken cancellationToken = default)
    {
        return await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Conversation>> GetAllConversationsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Conversations
            .Include(c => c.Messages)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task ClearConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conv != null)
        {
            _db.Conversations.Remove(conv);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

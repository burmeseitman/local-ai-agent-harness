using System.Collections.Concurrent;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Memory;

public class MemoryService : IMemoryService
{
    private readonly ConcurrentDictionary<string, MemoryItem> _store = new();

    public MemoryService(IEnumerable<MemoryItem>? seedItems = null)
    {
        if (seedItems != null)
        {
            foreach (var item in seedItems)
            {
                _store[item.Id] = item;
            }
        }
    }

    public Task<MemoryItem> AddMemoryAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString();
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;

        _store[item.Id] = item;
        return Task.FromResult(item);
    }

    public Task<List<MemoryItem>> SearchMemoryAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(_store.Values
                .OrderByDescending(m => m.Importance)
                .Take(topK)
                .ToList());
        }

        var terms = query.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToHashSet();

        var scored = _store.Values.Select(m =>
        {
            double score = 0;
            var contentLower = m.Content.ToLowerInvariant();

            foreach (var term in terms)
            {
                if (contentLower.Contains(term)) score += 2.0;
                if (m.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase))) score += 3.0;
            }

            // Weight with importance
            score *= m.Importance;

            return (Item: m, Score: score);
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .ThenByDescending(x => x.Item.Importance)
        .Take(topK)
        .Select(x => x.Item)
        .ToList();

        return Task.FromResult(scored);
    }

    public Task<List<MemoryItem>> GetAllMemoriesAsync(MemoryType? type = null, CancellationToken cancellationToken = default)
    {
        var items = _store.Values.AsEnumerable();
        if (type.HasValue)
        {
            items = items.Where(m => m.Type == type.Value);
        }

        return Task.FromResult(items.OrderByDescending(m => m.UpdatedAt).ToList());
    }

    public Task<bool> DeleteMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    public async Task ExtractAndStoreCandidatesAsync(
        string conversationId,
        string userPrompt,
        string agentResponse,
        CancellationToken cancellationToken = default)
    {
        // Simple heuristic memory candidate extraction
        var lowerPrompt = userPrompt.ToLowerInvariant();
        var lowerResp = agentResponse.ToLowerInvariant();

        if (lowerPrompt.Contains("remember") || lowerPrompt.Contains("prefer") || lowerPrompt.Contains("rule") || lowerPrompt.Contains("note"))
        {
            await AddMemoryAsync(new MemoryItem
            {
                Type = MemoryType.LongTerm,
                Content = $"User preference: {userPrompt}",
                Importance = 4.5,
                Tags = new List<string> { "user-preference", "explicit-instruction" },
                Source = $"Conversation:{conversationId}"
            }, cancellationToken);
        }
        else if (lowerResp.Contains("error") || lowerResp.Contains("failed") || lowerResp.Contains("blocked"))
        {
            await AddMemoryAsync(new MemoryItem
            {
                Type = MemoryType.Episodic,
                Content = $"Incident episode: Query '{userPrompt}' encountered: {agentResponse[..Math.Min(120, agentResponse.Length)]}",
                Importance = 3.0,
                Tags = new List<string> { "incident-learning", "error-recovery" },
                Source = $"RunEpisode:{conversationId}"
            }, cancellationToken);
        }
    }
}

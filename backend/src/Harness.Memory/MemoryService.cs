using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Memory;

public class MemoryService : IMemoryService
{
    private readonly ConcurrentDictionary<string, MemoryItem> _store = new();
    private readonly IMemoryStore? _persistentStore;

    public MemoryService(IMemoryStore? persistentStore = null, IEnumerable<MemoryItem>? seedItems = null)
    {
        _persistentStore = persistentStore;
        if (seedItems != null)
        {
            foreach (var item in seedItems)
            {
                _store[item.Id] = item;
            }
        }
    }

    public async Task<MemoryItem> AddMemoryAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString();
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;

        _store[item.Id] = item;

        if (_persistentStore != null)
        {
            await _persistentStore.SaveAsync(item, cancellationToken);
        }

        return item;
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

    public async Task<bool> DeleteMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        var removed = _store.TryRemove(id, out _);
        if (_persistentStore != null)
        {
            var dbRemoved = await _persistentStore.DeleteAsync(id, cancellationToken);
            return removed || dbRemoved;
        }
        return removed;
    }

    private static readonly Regex PreferenceRegex = new(
        @"\b(remember\s+that|please\s+remember|my\s+preference\s+is|i\s+prefer|user\s+rule:)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MaliciousInjectionPattern = new(
        @"(system\s+prompt|ignore\s+previous|disregard|you\s+are\s+now|<script>|rm\s+-rf|DROP\s+TABLE|DELETE\s+FROM)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task ExtractAndStoreCandidatesAsync(
        string conversationId,
        string userPrompt,
        string agentResponse,
        CancellationToken cancellationToken = default)
    {
        // Guard: Do not store any candidate that contains obvious prompt injection or destructive command patterns
        if (MaliciousInjectionPattern.IsMatch(userPrompt) || MaliciousInjectionPattern.IsMatch(agentResponse))
        {
            return;
        }

        var lowerPrompt = userPrompt.ToLowerInvariant();
        var lowerResp = agentResponse.ToLowerInvariant();

        // Exact boundary matching instead of naive substring matching
        if (PreferenceRegex.IsMatch(userPrompt))
        {
            await AddMemoryAsync(new MemoryItem
            {
                Type = MemoryType.LongTerm,
                Content = $"User preference: {userPrompt.Trim()}",
                Importance = 4.0,
                Tags = new List<string> { "user-preference", "explicit-instruction" },
                Source = $"Conversation:{conversationId}"
            }, cancellationToken);
        }
        else if (lowerResp.Contains("error:") || lowerResp.Contains("blocked:") || lowerResp.Contains("failed:"))
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

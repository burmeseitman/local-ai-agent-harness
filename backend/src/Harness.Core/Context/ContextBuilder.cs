using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Core.Context;

public class ContextBuilder : IContextBuilder
{
    private readonly IMemoryService _memoryService;
    private readonly IConversationManager _conversationManager;

    public ContextBuilder(
        IMemoryService memoryService,
        IConversationManager conversationManager)
    {
        _memoryService = memoryService;
        _conversationManager = conversationManager;
    }

    public async Task<List<ContextItem>> BuildContextAsync(
        string userPrompt,
        string conversationId,
        List<ToolDefinition> availableTools,
        CancellationToken cancellationToken = default)
    {
        var context = new List<ContextItem>();

        // 1. System Guardrails & Prompt Instructions
        var systemInstructions = """
        You are an autonomous AI Agent operating inside the Secure Local AI Agent Harness.
        SECURITY & OPERATIONAL POLICIES:
        1. You have access to specific registered tools with defined schemas.
        2. Always inspect your tool options before acting.
        3. Never attempt to execute raw shell, SQL, or destructive filesystem commands.
        4. When a tool call is executed, its result is verified and returned to you.
        5. Formulate concise, accurate conclusions based only on validated tool outputs.
        6. If a tool request is blocked by policy, explain the security guardrail politely to the user.
        """;

        context.Add(new ContextItem
        {
            Source = "HarnessGuardrails",
            Type = "SystemPrompt",
            Relevance = 1.0,
            Content = systemInstructions,
            EstimatedTokens = EstimateTokens(systemInstructions)
        });

        // 2. Retrieve Relevant Long-Term / Episodic Memory
        var relevantMemories = await _memoryService.SearchMemoryAsync(userPrompt, topK: 3, cancellationToken);
        foreach (var mem in relevantMemories)
        {
            var content = $"[Memory ({mem.Type}) | Importance: {mem.Importance:F1} | Tags: {string.Join(',', mem.Tags)}]: {mem.Content}";
            context.Add(new ContextItem
            {
                Source = $"MemoryStore:{mem.Id}",
                Type = "Memory",
                Relevance = Math.Clamp(mem.Importance / 5.0, 0.5, 1.0),
                Content = content,
                EstimatedTokens = EstimateTokens(content)
            });
        }

        // 3. Current Conversation History (limited to recent turns for context efficiency)
        if (!string.IsNullOrEmpty(conversationId))
        {
            var history = await _conversationManager.GetHistoryAsync(conversationId, limit: 6, cancellationToken);
            foreach (var msg in history.Where(m => m.Content != userPrompt))
            {
                var content = $"[{msg.Role}]: {msg.Content}";
                context.Add(new ContextItem
                {
                    Source = "ConversationHistory",
                    Type = "History",
                    Relevance = 0.8,
                    Content = content,
                    EstimatedTokens = EstimateTokens(content)
                });
            }
        }

        // 4. Relevant Available Tools (filtered or full catalog based on relevance)
        var toolsSummary = availableTools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            risk = t.RiskLevel.ToString()
        });

        var toolsJson = JsonSerializer.Serialize(toolsSummary, new JsonSerializerOptions { WriteIndented = false });
        context.Add(new ContextItem
        {
            Source = "ToolRegistry",
            Type = "ToolCatalog",
            Relevance = 0.95,
            Content = $"Available Harness Tools: {toolsJson}",
            EstimatedTokens = EstimateTokens(toolsJson)
        });

        // 5. Current User Request
        context.Add(new ContextItem
        {
            Source = "UserPrompt",
            Type = "CurrentTask",
            Relevance = 1.0,
            Content = $"User Request: {userPrompt}",
            EstimatedTokens = EstimateTokens(userPrompt)
        });

        return context;
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}

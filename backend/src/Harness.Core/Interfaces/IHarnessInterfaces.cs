using System.Text.Json;
using Harness.Models;

namespace Harness.Core.Interfaces;

public class ModelCompletionResult
{
    public string Content { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; set; } = new();
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public double LatencyMs { get; set; }
    public bool HasToolCalls => ToolCalls.Count > 0;
    public bool IsFallback { get; set; } = false;
    public string? FallbackReason { get; set; }
}

public interface IAiModelProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    Task<ModelCompletionResult> GenerateAsync(
        List<AgentMessage> messages,
        List<ToolDefinition>? availableTools = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(
        List<AgentMessage> messages,
        CancellationToken cancellationToken = default);
}

public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolRiskLevel RiskLevel { get; }
    JsonDocument InputSchema { get; }
    JsonDocument OutputSchema { get; }
    Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default);
}

public interface IToolRegistry
{
    void RegisterTool(ITool tool);
    ITool? GetTool(string name);
    IReadOnlyList<ITool> GetAllTools();
    bool HasTool(string name);
    List<ToolDefinition> GetDefinitions();
}

public interface IPolicyEngine
{
    Task<PolicyDecision> EvaluatePolicyAsync(ITool tool, string argumentsJson, CancellationToken cancellationToken = default);
}

public interface IInputValidator
{
    ValidationResult Validate(ITool tool, string argumentsJson);
}

public interface IOutputValidator
{
    ValidationResult Validate(ITool tool, ToolResult result);
}

public interface IAgentRunStore
{
    Task CreateRunAsync(AgentRun run, CancellationToken cancellationToken = default);
    Task UpdateRunAsync(AgentRun run, CancellationToken cancellationToken = default);
}

public interface IToolExecutionStore
{
    Task RecordExecutionAsync(ToolExecutionRecord record, CancellationToken cancellationToken = default);
}

public interface IToolExecutor
{
    Task<ToolResult> ExecuteToolCallAsync(ToolCall call, string runId, CancellationToken cancellationToken = default);
}

public interface IRetryPolicy
{
    Task<T> ExecuteWithRetryAsync<T>(
        Func<int, Task<T>> operation,
        Func<T, bool> shouldRetry,
        int maxRetries = 2,
        TimeSpan? initialDelay = null,
        CancellationToken cancellationToken = default);
}

public interface IMemoryStore
{
    Task<MemoryItem> SaveAsync(MemoryItem item, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<List<MemoryItem>> LoadAllAsync(CancellationToken cancellationToken = default);
}

public interface IMemoryService
{
    Task<MemoryItem> AddMemoryAsync(MemoryItem item, CancellationToken cancellationToken = default);
    Task<List<MemoryItem>> SearchMemoryAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
    Task<List<MemoryItem>> GetAllMemoriesAsync(MemoryType? type = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteMemoryAsync(string id, CancellationToken cancellationToken = default);
    Task ExtractAndStoreCandidatesAsync(string conversationId, string userPrompt, string agentResponse, CancellationToken cancellationToken = default);
}

public interface IContextBuilder
{
    Task<List<ContextItem>> BuildContextAsync(
        string userPrompt,
        string conversationId,
        List<ToolDefinition> availableTools,
        CancellationToken cancellationToken = default);
}

public interface IExecutionTracer
{
    void RecordEvent(string runId, string conversationId, string operation, double latencyMs, string? details = null, string status = "OK");
    List<ExecutionTrace> GetTracesForRun(string runId);
    List<ExecutionTrace> GetAllTraces(int limit = 100);
    long GetRetryCount();
}

public interface IConversationManager
{
    Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default);
    Task<Conversation> GetOrCreateConversationAsync(string? conversationId, CancellationToken cancellationToken = default);
    Task<AgentMessage> AddMessageAsync(string conversationId, MessageRole role, string content, Dictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
    Task<List<AgentMessage>> GetHistoryAsync(string conversationId, int limit = 20, CancellationToken cancellationToken = default);
    Task<List<Conversation>> GetAllConversationsAsync(CancellationToken cancellationToken = default);
    Task ClearConversationAsync(string conversationId, CancellationToken cancellationToken = default);
}

public interface IHarnessRunner
{
    /// <param name="onStep">
    /// Awaited after every recorded step (e.g. to stream SSE events). Exceptions thrown by
    /// the callback propagate to the caller.
    /// </param>
    Task<AgentResponse> RunAsync(
        AgentRequest request,
        Func<AgentStep, Task>? onStep = null,
        CancellationToken cancellationToken = default);
}

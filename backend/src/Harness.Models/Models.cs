using System.Text.Json;

namespace Harness.Models;

public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ToolRiskLevel RiskLevel { get; set; } = ToolRiskLevel.LOW;
    public JsonDocument? InputSchema { get; set; }
    public JsonDocument? OutputSchema { get; set; }
}

public class ToolCall
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ToolResult
{
    public string ToolCallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string OutputJson { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public double ExecutionDurationMs { get; set; }
    public ToolRiskLevel RiskLevel { get; set; } = ToolRiskLevel.LOW;
    public ToolExecutionStatus Status { get; set; } = ToolExecutionStatus.Success;
}

public class PolicyDecision
{
    public PolicyDecisionType Decision { get; set; } = PolicyDecisionType.ALLOWED;
    public string ToolName { get; set; } = string.Empty;
    public ToolRiskLevel RiskLevel { get; set; } = ToolRiskLevel.LOW;
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ValidationResult
{
    public ValidationOutcome Outcome { get; set; } = ValidationOutcome.Valid;
    public bool IsValid => Outcome == ValidationOutcome.Valid;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }

    public static ValidationResult Success(string message = "Validation passed") =>
        new() { Outcome = ValidationOutcome.Valid, Message = message };

    public static ValidationResult Fail(ValidationOutcome outcome, string message, string? details = null) =>
        new() { Outcome = outcome, Message = message, Details = details };
}

public class MemoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MemoryType Type { get; set; } = MemoryType.ShortTerm;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public double Importance { get; set; } = 1.0;
    public List<string> Tags { get; set; } = new();
    public string Source { get; set; } = "system";
    public float[]? Embedding { get; set; }
}

public class ContextItem
{
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Relevance { get; set; } = 1.0;
    public string Content { get; set; } = string.Empty;
    public int EstimatedTokens { get; set; }
}

public class AgentMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public MessageRole Role { get; set; } = MessageRole.USER;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class AgentStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RunId { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public AgentStepType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double DurationMs { get; set; }
    public string Status { get; set; } = "Success"; // Success, Failed, Skipped, Blocked
    public string? InputPayload { get; set; }
    public string? OutputPayload { get; set; }
    public string? ErrorDetails { get; set; }
}

public class AgentRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public RunStatus Status { get; set; } = RunStatus.Running;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public double LatencyMs { get; set; }
    public int LoopCount { get; set; }
    public string? FinalAnswer { get; set; }
    public string? ErrorMessage { get; set; }
    public List<AgentStep> Steps { get; set; } = new();
}

public class AgentRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public string? SessionId { get; set; }
    public bool StreamEvents { get; set; } = false;
}

public class AgentResponse
{
    public string RunId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string FinalAnswer { get; set; } = string.Empty;
    public RunStatus Status { get; set; } = RunStatus.Completed;
    public double LatencyMs { get; set; }
    public int LoopCount { get; set; }
    public List<AgentStep> Steps { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class ExecutionTrace
{
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");
    public string RunId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Operation { get; set; } = string.Empty;
    public double LatencyMs { get; set; }
    public string? Details { get; set; }
    public string Status { get; set; } = "OK";
}

public class HarnessMetrics
{
    public long TotalRuns { get; set; }
    public long SuccessfulRuns { get; set; }
    public long FailedRuns { get; set; }
    public double AverageLatencyMs { get; set; }
    public long ToolCalls { get; set; }
    public long ToolFailures { get; set; }
    public long Retries { get; set; }
    public double AverageLoopCount { get; set; }
}

public class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Conversation";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<AgentMessage> Messages { get; set; } = new();
}

public class ToolExecutionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RunId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public ToolRiskLevel RiskLevel { get; set; }
    public string ArgumentsJson { get; set; } = "{}";
    public string? OutputJson { get; set; }
    public bool IsSuccess { get; set; }
    public double DurationMs { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public PolicyDecisionType PolicyDecision { get; set; }
}

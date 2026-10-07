using System.Text.Json.Serialization;

namespace Harness.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ToolRiskLevel
{
    LOW,
    MEDIUM,
    HIGH,
    CRITICAL
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ToolExecutionStatus
{
    Pending,
    Executing,
    Success,
    Failed,
    Blocked,
    AwaitingApproval
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolicyDecisionType
{
    ALLOWED,
    BLOCKED,
    REQUIRES_APPROVAL
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentStepType
{
    PLAN,
    ACT,
    OBSERVE,
    EVALUATE,
    RETRY,
    COMPLETE,
    FAIL
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemoryType
{
    ShortTerm,
    LongTerm,
    Episodic,
    Semantic
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationOutcome
{
    Valid,
    Invalid,
    Retryable,
    Fatal
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageRole
{
    SYSTEM,
    USER,
    TOOL,
    MEMORY,
    MODEL_OUTPUT
}

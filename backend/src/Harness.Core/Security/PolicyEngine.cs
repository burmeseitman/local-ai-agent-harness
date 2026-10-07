using System.Text.RegularExpressions;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Core.Security;

public class PolicyEngineOptions
{
    public bool AutoExecuteLowRisk { get; set; } = true;
    public bool AutoExecuteMediumRisk { get; set; } = true;
    public bool RequireApprovalHighRisk { get; set; } = true;
    public bool RequireApprovalCriticalRisk { get; set; } = true;
    public int MaxArgumentSizeBytes { get; set; } = 16384;
}

public class PolicyEngine : IPolicyEngine
{
    private readonly PolicyEngineOptions _options;

    private static readonly Regex DangerousPatterns = new(
        @"(rm\s+-rf|DROP\s+TABLE|DELETE\s+FROM|UNION\s+SELECT|;\s*--|\|\s*bash|\|\s*sh|/bin/sh|powershell\.exe|\bexec\s*\(|<script>|IGNORE\s+ALL\s+PREVIOUS\s+INSTRUCTIONS)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PolicyEngine(PolicyEngineOptions? options = null)
    {
        _options = options ?? new PolicyEngineOptions();
    }

    public Task<PolicyDecision> EvaluatePolicyAsync(ITool tool, string argumentsJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // 1. Argument Size Validation
        if (!string.IsNullOrEmpty(argumentsJson) && argumentsJson.Length > _options.MaxArgumentSizeBytes)
        {
            return Task.FromResult(new PolicyDecision
            {
                Decision = PolicyDecisionType.BLOCKED,
                ToolName = tool.Name,
                RiskLevel = tool.RiskLevel,
                Reason = $"Security Block: Tool arguments exceeded max allowed limit of {_options.MaxArgumentSizeBytes} bytes.",
                Timestamp = DateTime.UtcNow
            });
        }

        // 2. Dangerous injection / malicious payload detection
        if (!string.IsNullOrEmpty(argumentsJson) && DangerousPatterns.IsMatch(argumentsJson))
        {
            var matched = DangerousPatterns.Match(argumentsJson).Value;
            return Task.FromResult(new PolicyDecision
            {
                Decision = PolicyDecisionType.BLOCKED,
                ToolName = tool.Name,
                RiskLevel = tool.RiskLevel,
                Reason = $"Security Violation: Dangerous pattern or prompt/code injection detected in tool arguments ('{matched}').",
                Timestamp = DateTime.UtcNow
            });
        }

        // 3. Risk Level Policy Evaluation
        switch (tool.RiskLevel)
        {
            case ToolRiskLevel.LOW:
                return Task.FromResult(new PolicyDecision
                {
                    Decision = _options.AutoExecuteLowRisk ? PolicyDecisionType.ALLOWED : PolicyDecisionType.REQUIRES_APPROVAL,
                    ToolName = tool.Name,
                    RiskLevel = tool.RiskLevel,
                    Reason = "Risk level LOW is automatically permitted by harness policy.",
                    Timestamp = DateTime.UtcNow
                });

            case ToolRiskLevel.MEDIUM:
                return Task.FromResult(new PolicyDecision
                {
                    Decision = _options.AutoExecuteMediumRisk ? PolicyDecisionType.ALLOWED : PolicyDecisionType.REQUIRES_APPROVAL,
                    ToolName = tool.Name,
                    RiskLevel = tool.RiskLevel,
                    Reason = "Risk level MEDIUM is permitted with standard audit logging.",
                    Timestamp = DateTime.UtcNow
                });

            case ToolRiskLevel.HIGH:
                return Task.FromResult(new PolicyDecision
                {
                    Decision = _options.RequireApprovalHighRisk ? PolicyDecisionType.BLOCKED : PolicyDecisionType.ALLOWED,
                    ToolName = tool.Name,
                    RiskLevel = tool.RiskLevel,
                    Reason = "Risk level HIGH requires explicit administrator approval and is blocked in automated runs.",
                    Timestamp = DateTime.UtcNow
                });

            case ToolRiskLevel.CRITICAL:
            default:
                return Task.FromResult(new PolicyDecision
                {
                    Decision = PolicyDecisionType.BLOCKED,
                    ToolName = tool.Name,
                    RiskLevel = tool.RiskLevel,
                    Reason = "Risk level CRITICAL is strictly blocked by automated harness security guardrails.",
                    Timestamp = DateTime.UtcNow
                });
        }
    }
}

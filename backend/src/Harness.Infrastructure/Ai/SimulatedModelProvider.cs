using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Infrastructure.Ai;

/// <summary>
/// Intelligent local simulator provider for testing and offline environments.
/// Understands user requests and selects appropriate tools or synthesizes final answers.
/// </summary>
public class SimulatedModelProvider : IAiModelProvider
{
    public string ProviderName => "Simulated";
    public string ModelName => "simulated-agent-v1";

    public Task<ModelCompletionResult> GenerateAsync(
        List<AgentMessage> messages,
        List<ToolDefinition>? availableTools = null,
        CancellationToken cancellationToken = default)
    {
        var lastUserMsg = messages.LastOrDefault(m => m.Role == MessageRole.USER)?.Content ?? "";
        var lastToolMsg = messages.LastOrDefault(m => m.Role == MessageRole.TOOL);

        // If we just got a tool result, formulate the final evaluation response!
        if (lastToolMsg != null)
        {
            var synthesized = SynthesizeFromToolResult(lastToolMsg.Content, lastUserMsg);
            return Task.FromResult(new ModelCompletionResult
            {
                Content = synthesized,
                ToolCalls = new List<ToolCall>(),
                PromptTokens = 120,
                CompletionTokens = 85,
                LatencyMs = 45
            });
        }

        // Determine if a tool should be called based on the prompt
        var lower = lastUserMsg.ToLowerInvariant();
        var toolCalls = new List<ToolCall>();
        string reasoning = "Analyzing user intent and selecting appropriate harness tool...";

        if (lower.Contains("delete") || lower.Contains("drop") || lower.Contains("block") || lower.Contains("shutdown") || lower.Contains("cluster"))
        {
            // Tool that triggers CRITICAL risk policy rejection
            toolCalls.Add(new ToolCall
            {
                ToolName = "delete_server_cluster",
                ArgumentsJson = "{\"target\": \"prod-db-cluster\"}"
            });
            reasoning = "Attempting cluster removal command.";
        }
        else if (lower.Contains("flaky") || lower.Contains("test retry") || lower.Contains("simulate failure") || lower.Contains("test flaky"))
        {
            toolCalls.Add(new ToolCall
            {
                ToolName = "flaky_tool_simulator",
                ArgumentsJson = "{\"retryCount\": 2}"
            });
            reasoning = "Simulating execution of a flaky external tool to verify retry policies.";
        }
        else if (lower.Contains("server") || lower.Contains("healthy") || lower.Contains("status") || lower.Contains("cpu"))
        {
            toolCalls.Add(new ToolCall
            {
                ToolName = "get_server_status",
                ArgumentsJson = "{}"
            });
            reasoning = "I need to check the server health and system metrics using get_server_status.";
        }
        else if (lower.Contains("calc") || lower.Contains("add") || lower.Contains("multiply") || lower.Contains("divide") || lower.Contains("+") || lower.Contains("*"))
        {
            // Simple expression detection
            toolCalls.Add(new ToolCall
            {
                ToolName = "calculator",
                ArgumentsJson = "{\"operation\": \"add\", \"a\": 42, \"b\": 58}"
            });
            reasoning = "Performing calculation using calculator tool.";
        }
        else if (lower.Contains("doc") || lower.Contains("connection failure") || lower.Contains("documentation"))
        {
            var query = lower.Contains("database") ? "database connection" : "configuration";
            toolCalls.Add(new ToolCall
            {
                ToolName = "search_documentation",
                ArgumentsJson = JsonSerializer.Serialize(new { query })
            });
            reasoning = $"Searching local system documentation for '{query}'.";
        }
        else if (lower.Contains("incident") || lower.Contains("history"))
        {
            toolCalls.Add(new ToolCall
            {
                ToolName = "get_incident_history",
                ArgumentsJson = JsonSerializer.Serialize(new { severity = "high" })
            });
            reasoning = "Retrieving incident history for analysis.";
        }
        else if (lower.Contains("log") || lower.Contains("system log"))
        {
            toolCalls.Add(new ToolCall
            {
                ToolName = "get_system_logs",
                ArgumentsJson = JsonSerializer.Serialize(new { level = "error", limit = 10 })
            });
            reasoning = "Inspecting recent system error logs.";
        }
        else if (lower.Contains("delete") || lower.Contains("drop") || lower.Contains("block") || lower.Contains("shutdown"))
        {
            // Tool that should trigger policy rejection or approval
            toolCalls.Add(new ToolCall
            {
                ToolName = "delete_server_cluster",
                ArgumentsJson = "{\"target\": \"prod-db-cluster\"}"
            });
            reasoning = "Attempting cluster removal command.";
        }
        else if (lower.Contains("fail") || lower.Contains("retry"))
        {
            toolCalls.Add(new ToolCall
            {
                ToolName = "flaky_tool_simulator",
                ArgumentsJson = "{\"retryCount\": 2}"
            });
            reasoning = "Simulating execution of a flaky external tool to verify retry policies.";
        }
        else
        {
            // Direct answer
            return Task.FromResult(new ModelCompletionResult
            {
                Content = $"I have received your request: '{lastUserMsg}'. I am the Secure Local AI Agent Harness ready to assist you safely.",
                ToolCalls = new List<ToolCall>(),
                PromptTokens = 50,
                CompletionTokens = 30,
                LatencyMs = 25
            });
        }

        return Task.FromResult(new ModelCompletionResult
        {
            Content = reasoning,
            ToolCalls = toolCalls,
            PromptTokens = 80,
            CompletionTokens = 40,
            LatencyMs = 35
        });
    }

    public async IAsyncEnumerable<string> StreamAsync(
        List<AgentMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var res = await GenerateAsync(messages, null, cancellationToken);
        var words = res.Content.Split(' ');
        foreach (var w in words)
        {
            yield return w + " ";
            await Task.Delay(25, cancellationToken);
        }
    }

    private static string SynthesizeFromToolResult(string toolOutput, string userPrompt)
    {
        return $"Based on the verified tool execution result:\n\n{toolOutput}\n\nTask '{userPrompt}' has been processed securely according to harness policy.";
    }
}

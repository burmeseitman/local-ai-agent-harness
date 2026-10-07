using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Harness.Core.Interfaces;
using Harness.Models;
using Microsoft.Extensions.Logging;

namespace Harness.Infrastructure.Ai;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    public int TimeoutSeconds { get; set; } = 60;
    public bool FallbackToSimulatedWhenUnavailable { get; set; } = true;
}

public class OllamaModelProvider : IAiModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaModelProvider> _logger;
    private readonly SimulatedModelProvider _simulatedFallback;

    public string ProviderName => "Ollama";
    public string ModelName => _options.Model;

    public OllamaModelProvider(
        HttpClient httpClient,
        OllamaOptions options,
        ILogger<OllamaModelProvider> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _simulatedFallback = new SimulatedModelProvider();
    }

    public async Task<ModelCompletionResult> GenerateAsync(
        List<AgentMessage> messages,
        List<ToolDefinition>? availableTools = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var ollamaMessages = messages.Select(m => new
            {
                role = MapRole(m.Role),
                content = m.Content
            }).ToList();

            object requestPayload;
            if (availableTools != null && availableTools.Count > 0)
            {
                var toolsFormatted = availableTools.Select(t => new
                {
                    type = "function",
                    function = new
                    {
                        name = t.Name,
                        description = t.Description,
                        parameters = t.InputSchema?.RootElement
                    }
                }).ToList();

                requestPayload = new
                {
                    model = _options.Model,
                    messages = ollamaMessages,
                    tools = toolsFormatted,
                    stream = false
                };
            }
            else
            {
                requestPayload = new
                {
                    model = _options.Model,
                    messages = ollamaMessages,
                    stream = false
                };
            }

            var response = await _httpClient.PostAsJsonAsync("/api/chat", requestPayload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var jsonDoc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken);

                var root = jsonDoc.RootElement;
                var messageElem = root.GetProperty("message");
                var content = messageElem.TryGetProperty("content", out var cProp) ? cProp.GetString() ?? "" : "";

                var toolCalls = new List<ToolCall>();
                if (messageElem.TryGetProperty("tool_calls", out var tcProp) && tcProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tc in tcProp.EnumerateArray())
                    {
                        if (tc.TryGetProperty("function", out var fn))
                        {
                            var toolName = fn.GetProperty("name").GetString() ?? "";
                            var argsJson = fn.TryGetProperty("arguments", out var args) ? args.GetRawText() : "{}";
                            toolCalls.Add(new ToolCall
                            {
                                ToolName = toolName,
                                ArgumentsJson = argsJson
                            });
                        }
                    }
                }

                // If native tool calls were empty, attempt robust extraction from text
                if (toolCalls.Count == 0 && availableTools != null && availableTools.Count > 0)
                {
                    var parsedCalls = TryExtractToolCallsFromText(content, availableTools);
                    toolCalls.AddRange(parsedCalls);
                }

                sw.Stop();
                int promptTokens = root.TryGetProperty("prompt_eval_count", out var pe) ? pe.GetInt32() : 0;
                int completionTokens = root.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0;

                return new ModelCompletionResult
                {
                    Content = content,
                    ToolCalls = toolCalls,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    LatencyMs = sw.Elapsed.TotalMilliseconds
                };
            }
            else
            {
                _logger.LogWarning("Ollama returned status code {StatusCode}. Falling back if enabled.", response.StatusCode);
                if (_options.FallbackToSimulatedWhenUnavailable)
                {
                    return await _simulatedFallback.GenerateAsync(messages, availableTools, cancellationToken);
                }
                throw new HttpRequestException($"Ollama API error: {response.StatusCode}");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to connect to Ollama at {BaseUrl}. Falling back to simulated provider.", _options.BaseUrl);
            if (_options.FallbackToSimulatedWhenUnavailable)
            {
                return await _simulatedFallback.GenerateAsync(messages, availableTools, cancellationToken);
            }
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        List<AgentMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Simple streaming yield
        var res = await GenerateAsync(messages, null, cancellationToken);
        var words = res.Content.Split(' ');
        foreach (var word in words)
        {
            yield return word + " ";
            await Task.Delay(20, cancellationToken);
        }
    }

    private static string MapRole(MessageRole role) => role switch
    {
        MessageRole.SYSTEM => "system",
        MessageRole.USER => "user",
        MessageRole.TOOL => "tool",
        MessageRole.MEMORY => "system",
        MessageRole.MODEL_OUTPUT => "assistant",
        _ => "user"
    };

    private static List<ToolCall> TryExtractToolCallsFromText(string text, List<ToolDefinition> tools)
    {
        var result = new List<ToolCall>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        // Pattern 1: ```json { "tool": "xyz", "arguments": { ... } } ```
        var jsonBlockRegex = new Regex(@"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        var match = jsonBlockRegex.Match(text);
        if (match.Success)
        {
            try
            {
                using var doc = JsonDocument.Parse(match.Groups[1].Value);
                var root = doc.RootElement;
                if (root.TryGetProperty("tool", out var tProp) || root.TryGetProperty("name", out tProp))
                {
                    var toolName = tProp.GetString();
                    if (!string.IsNullOrEmpty(toolName) && tools.Any(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var args = root.TryGetProperty("arguments", out var aProp) || root.TryGetProperty("parameters", out aProp)
                            ? aProp.GetRawText()
                            : "{}";

                        result.Add(new ToolCall
                        {
                            ToolName = toolName,
                            ArgumentsJson = args
                        });
                        return result;
                    }
                }
            }
            catch
            {
                // Ignore parse errors in fallback
            }
        }

        return result;
    }
}

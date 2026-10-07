using System.Diagnostics;
using Harness.Core.Interfaces;
using Harness.Core.Loop;
using Harness.Models;
using Microsoft.Extensions.Logging;

namespace Harness.Core.Runner;

public class HarnessRunner : IHarnessRunner
{
    private readonly IContextBuilder _contextBuilder;
    private readonly IToolRegistry _toolRegistry;
    private readonly AgentLoop _agentLoop;
    private readonly IMemoryService _memoryService;
    private readonly IConversationManager _conversationManager;
    private readonly IExecutionTracer _tracer;
    private readonly ILogger<HarnessRunner> _logger;

    public HarnessRunner(
        IContextBuilder contextBuilder,
        IToolRegistry toolRegistry,
        AgentLoop agentLoop,
        IMemoryService memoryService,
        IConversationManager conversationManager,
        IExecutionTracer tracer,
        ILogger<HarnessRunner> logger)
    {
        _contextBuilder = contextBuilder;
        _toolRegistry = toolRegistry;
        _agentLoop = agentLoop;
        _memoryService = memoryService;
        _conversationManager = conversationManager;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task<AgentResponse> RunAsync(
        AgentRequest request,
        Action<AgentStep>? onStep = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();

        var runId = Guid.NewGuid().ToString("N");
        var conversation = await _conversationManager.GetOrCreateConversationAsync(request.ConversationId, cancellationToken);
        var conversationId = conversation.Id;

        _logger.LogInformation("Initiating Harness Run {RunId} in Conversation {ConvId}", runId, conversationId);
        _tracer.RecordEvent(runId, conversationId, "RUN_INITIATED", 0, $"User prompt: {request.Prompt}");

        // 1. Persist User Prompt Message
        await _conversationManager.AddMessageAsync(conversationId, MessageRole.USER, request.Prompt, cancellationToken: cancellationToken);

        // 2. Build Rich Context (Prompts, Memory, Tools, Guardrails)
        var availableTools = _toolRegistry.GetDefinitions();
        var contextItems = await _contextBuilder.BuildContextAsync(request.Prompt, conversationId, availableTools, cancellationToken);
        _tracer.RecordEvent(runId, conversationId, "CONTEXT_BUILT", 0, $"Aggregated {contextItems.Count} context items");

        // 3. Prepare Working Messages for AgentLoop
        var messages = new List<AgentMessage>();

        // System prompt context
        var systemItem = contextItems.FirstOrDefault(c => c.Type == "SystemPrompt");
        if (systemItem != null)
        {
            messages.Add(new AgentMessage { Role = MessageRole.SYSTEM, Content = systemItem.Content });
        }

        // Memory items context
        foreach (var mem in contextItems.Where(c => c.Type == "Memory"))
        {
            messages.Add(new AgentMessage { Role = MessageRole.MEMORY, Content = mem.Content });
        }

        // Historical turns
        var history = await _conversationManager.GetHistoryAsync(conversationId, limit: 10, cancellationToken);
        foreach (var h in history.Where(m => m.Content != request.Prompt))
        {
            messages.Add(h);
        }

        // Current user task
        messages.Add(new AgentMessage { Role = MessageRole.USER, Content = request.Prompt });

        // 4. Initialize State & Run AgentLoop
        var state = new AgentState
        {
            RunId = runId,
            ConversationId = conversationId,
            UserPrompt = request.Prompt,
            Messages = messages,
            AvailableTools = availableTools,
            MaxIterations = 10
        };

        var finalState = await _agentLoop.ExecuteAsync(state, onStep, cancellationToken);
        sw.Stop();

        var finalAnswer = finalState.FinalAnswer ?? finalState.ErrorMessage ?? "Agent run ended without a response.";

        // 5. Persist Final Agent Response & Extract Long-Term Memory Candidate
        await _conversationManager.AddMessageAsync(conversationId, MessageRole.MODEL_OUTPUT, finalAnswer, cancellationToken: cancellationToken);

        try
        {
            await _memoryService.ExtractAndStoreCandidatesAsync(conversationId, request.Prompt, finalAnswer, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background memory candidate extraction failed for Run {RunId}", runId);
        }

        _tracer.RecordEvent(runId, conversationId, "RUN_FINISHED", sw.Elapsed.TotalMilliseconds,
            $"Final status: {finalState.Status}, Iterations: {finalState.CurrentIteration}",
            finalState.Status.ToString());

        return new AgentResponse
        {
            RunId = runId,
            ConversationId = conversationId,
            FinalAnswer = finalAnswer,
            Status = finalState.Status,
            LatencyMs = sw.Elapsed.TotalMilliseconds,
            LoopCount = finalState.CurrentIteration,
            Steps = finalState.Steps,
            ErrorMessage = finalState.ErrorMessage
        };
    }
}

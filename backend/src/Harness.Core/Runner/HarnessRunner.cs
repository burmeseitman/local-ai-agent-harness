using System.Diagnostics;
using Harness.Core.Interfaces;
using Harness.Core.Loop;
using Harness.Models;
using Microsoft.Extensions.Logging;

namespace Harness.Core.Runner;

public class HarnessRunnerOptions
{
    public int MaxIterations { get; set; } = 10;
}

public class HarnessRunner : IHarnessRunner
{
    private readonly IContextBuilder _contextBuilder;
    private readonly IToolRegistry _toolRegistry;
    private readonly AgentLoop _agentLoop;
    private readonly IMemoryService _memoryService;
    private readonly IConversationManager _conversationManager;
    private readonly IExecutionTracer _tracer;
    private readonly ILogger<HarnessRunner> _logger;
    private readonly IAgentRunStore? _runStore;
    private readonly HarnessRunnerOptions _options;

    public HarnessRunner(
        IContextBuilder contextBuilder,
        IToolRegistry toolRegistry,
        AgentLoop agentLoop,
        IMemoryService memoryService,
        IConversationManager conversationManager,
        IExecutionTracer tracer,
        ILogger<HarnessRunner> logger,
        IAgentRunStore? runStore = null,
        HarnessRunnerOptions? options = null)
    {
        _contextBuilder = contextBuilder;
        _toolRegistry = toolRegistry;
        _agentLoop = agentLoop;
        _memoryService = memoryService;
        _conversationManager = conversationManager;
        _tracer = tracer;
        _logger = logger;
        _runStore = runStore;
        _options = options ?? new HarnessRunnerOptions();
    }

    public async Task<AgentResponse> RunAsync(
        AgentRequest request,
        Func<AgentStep, Task>? onStep = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();

        var runId = Guid.NewGuid().ToString("N");
        var conversation = await _conversationManager.GetOrCreateConversationAsync(request.ConversationId, cancellationToken);
        var conversationId = conversation.Id;

        _logger.LogInformation("Initiating Harness Run {RunId} in Conversation {ConvId}", runId, conversationId);
        _tracer.RecordEvent(runId, conversationId, "RUN_INITIATED", 0, $"User prompt: {request.Prompt}");

        var runEntity = new AgentRun
        {
            Id = runId,
            ConversationId = conversationId,
            UserPrompt = request.Prompt,
            Status = RunStatus.Running,
            StartTime = DateTime.UtcNow
        };

        if (_runStore != null)
        {
            try
            {
                await _runStore.CreateRunAsync(runEntity, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist initial AgentRun {RunId}", runId);
            }
        }

        // 1. Persist User Prompt Message
        var userMessage = await _conversationManager.AddMessageAsync(conversationId, MessageRole.USER, request.Prompt, cancellationToken: cancellationToken);

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

        // Historical turns (most recent, chronological). Exclude the message we just
        // persisted by Id — it is appended explicitly below as the current task.
        var history = await _conversationManager.GetHistoryAsync(conversationId, limit: 10, cancellationToken);
        foreach (var h in history.Where(m => m.Id != userMessage?.Id))
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
            MaxIterations = _options.MaxIterations
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

        runEntity.Status = finalState.Status;
        runEntity.EndTime = DateTime.UtcNow;
        runEntity.LatencyMs = sw.Elapsed.TotalMilliseconds;
        runEntity.LoopCount = finalState.CurrentIteration;
        runEntity.FinalAnswer = finalAnswer;
        runEntity.ErrorMessage = finalState.ErrorMessage;
        runEntity.Steps = finalState.Steps;

        if (_runStore != null)
        {
            try
            {
                await _runStore.UpdateRunAsync(runEntity, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update completed AgentRun {RunId}", runId);
            }
        }

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

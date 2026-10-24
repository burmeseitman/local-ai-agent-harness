using Harness.Core.Context;
using Harness.Core.Interfaces;
using Harness.Core.Loop;
using Harness.Core.Resilience;
using Harness.Core.Runner;
using Harness.Core.Security;
using Harness.Core.Tools;
using Harness.Core.Validation;
using Harness.Infrastructure.Ai;
using Harness.Infrastructure.Data;
using Harness.Memory;
using Harness.Observability;
using Harness.Tools;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Database (SQLite default, PostgreSQL-ready)
var connString = builder.Configuration["CONNECTION_STRING"] ?? "Data Source=secure_harness.db";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connString);
});

// 3. AI Model Provider Configuration
var aiProvider = builder.Configuration["AI_PROVIDER"] ?? "ollama";
var ollamaBaseUrl = builder.Configuration["OLLAMA_BASE_URL"] ?? "http://localhost:11434";
var ollamaModel = builder.Configuration["OLLAMA_MODEL"] ?? "llama3.2";

builder.Services.AddHttpClient<OllamaModelProvider>();
builder.Services.AddSingleton(new OllamaOptions
{
    BaseUrl = ollamaBaseUrl,
    Model = ollamaModel,
    TimeoutSeconds = 45,
    FallbackToSimulatedWhenUnavailable = true
});

if (aiProvider.Equals("simulated", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAiModelProvider, SimulatedModelProvider>();
}
else
{
    builder.Services.AddSingleton<IAiModelProvider>(sp =>
    {
        var client = sp.GetRequiredService<HttpClient>();
        var opts = sp.GetRequiredService<OllamaOptions>();
        var logger = sp.GetRequiredService<ILogger<OllamaModelProvider>>();
        return new OllamaModelProvider(client, opts, logger);
    });
}

// 4. Observability & Tracing
builder.Services.AddSingleton<IExecutionTracer, ExecutionTracer>();

// 5. Tools & Tool Registry
builder.Services.AddSingleton<ITool, GetServerStatusTool>();
builder.Services.AddSingleton<ITool, CalculatorTool>();
builder.Services.AddSingleton<ITool, SearchDocumentationTool>();
builder.Services.AddSingleton<ITool, GetSystemLogsTool>();
builder.Services.AddSingleton<ITool, GetIncidentHistoryTool>();
builder.Services.AddSingleton<ITool, FlakySimulatorTool>();
builder.Services.AddSingleton<ITool, DeleteClusterTool>();

builder.Services.AddSingleton<IToolRegistry>(sp =>
{
    var tools = sp.GetServices<ITool>();
    return new ToolRegistry(tools);
});

// 6. Security, Validation & Resilience
var autoLow = bool.TryParse(builder.Configuration["ENABLE_AUTO_EXECUTION_LOW_RISK"], out var al) ? al : true;
var autoMed = bool.TryParse(builder.Configuration["ENABLE_AUTO_EXECUTION_MEDIUM_RISK"], out var am) ? am : true;
var reqAppHigh = bool.TryParse(builder.Configuration["REQUIRE_APPROVAL_HIGH_RISK"], out var rah) ? rah : true;
var toolTimeoutSec = int.TryParse(builder.Configuration["TOOL_EXECUTION_TIMEOUT_SECONDS"], out var tts) ? tts : 30;
var maxResultBytes = int.TryParse(builder.Configuration["MAX_TOOL_RESULT_BYTES"], out var mrb) ? mrb : 65536;

builder.Services.AddSingleton<IPolicyEngine>(new PolicyEngine(new PolicyEngineOptions
{
    AutoExecuteLowRisk = autoLow,
    AutoExecuteMediumRisk = autoMed,
    RequireApprovalHighRisk = reqAppHigh
}));
builder.Services.AddSingleton<IInputValidator, InputValidator>();
builder.Services.AddSingleton<IOutputValidator>(new OutputValidator(new OutputValidatorOptions
{
    MaxResultSizeBytes = maxResultBytes
}));
builder.Services.AddSingleton<IRetryPolicy, RetryPolicy>();
builder.Services.AddScoped<IToolExecutionStore, DbToolExecutionStore>();
builder.Services.AddScoped<IToolExecutor>(sp => new ToolExecutor(
    sp.GetRequiredService<IToolRegistry>(),
    sp.GetRequiredService<IPolicyEngine>(),
    sp.GetRequiredService<IOutputValidator>(),
    sp.GetRequiredService<IRetryPolicy>(),
    sp.GetRequiredService<IExecutionTracer>(),
    sp.GetRequiredService<ILogger<ToolExecutor>>(),
    sp.GetRequiredService<IInputValidator>(),
    sp.GetRequiredService<IToolExecutionStore>(),
    new ToolExecutorOptions { ExecutionTimeoutSeconds = toolTimeoutSec }
));

// 7. Memory & Context
builder.Services.AddScoped<IMemoryStore, DbMemoryStore>();
builder.Services.AddSingleton<IMemoryService>(sp =>
{
    using var scope = sp.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<IMemoryStore>();
    var existingMemories = store.LoadAllAsync().GetAwaiter().GetResult();
    return new MemoryService(store, existingMemories);
});
builder.Services.AddScoped<IConversationManager, ConversationManager>();
builder.Services.AddScoped<IContextBuilder, ContextBuilder>();

// 8. Agent Loop & Harness Runner
var maxLoopIters = int.TryParse(builder.Configuration["MAX_AGENT_LOOP_ITERATIONS"], out var mli) ? mli : 10;
builder.Services.AddScoped<IAgentRunStore, DbAgentRunStore>();
builder.Services.AddScoped<AgentLoop>();
builder.Services.AddScoped<IHarnessRunner>(sp => new HarnessRunner(
    sp.GetRequiredService<IContextBuilder>(),
    sp.GetRequiredService<IToolRegistry>(),
    sp.GetRequiredService<AgentLoop>(),
    sp.GetRequiredService<IMemoryService>(),
    sp.GetRequiredService<IConversationManager>(),
    sp.GetRequiredService<IExecutionTracer>(),
    sp.GetRequiredService<ILogger<HarnessRunner>>(),
    sp.GetRequiredService<IAgentRunStore>(),
    new HarnessRunnerOptions { MaxIterations = maxLoopIters }
));

// 9. MVC & CORS & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Secure Local AI Agent Harness API",
        Version = "v1",
        Description = "Production-grade reference implementation of an educational AI Harness runtime."
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:8080" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 10. Database Migration & Seeding
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

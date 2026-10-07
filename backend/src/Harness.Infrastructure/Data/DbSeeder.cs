using Harness.Models;
using Microsoft.EntityFrameworkCore;

namespace Harness.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Conversations.AnyAsync())
        {
            return; // Already seeded
        }

        // 1. Seed 5 Sample Conversations
        var c1 = new Conversation { Id = "conv-101", Title = "Server Health Check & Diagnostics", CreatedAt = DateTime.UtcNow.AddHours(-12), UpdatedAt = DateTime.UtcNow.AddHours(-12) };
        var c2 = new Conversation { Id = "conv-102", Title = "Database Pool Starvation Inquiry", CreatedAt = DateTime.UtcNow.AddHours(-8), UpdatedAt = DateTime.UtcNow.AddHours(-8) };
        var c3 = new Conversation { Id = "conv-103", Title = "Cluster Deletion Guardrail Test", CreatedAt = DateTime.UtcNow.AddHours(-5), UpdatedAt = DateTime.UtcNow.AddHours(-5) };
        var c4 = new Conversation { Id = "conv-104", Title = "Transient Gateway Retry Test", CreatedAt = DateTime.UtcNow.AddHours(-3), UpdatedAt = DateTime.UtcNow.AddHours(-3) };
        var c5 = new Conversation { Id = "conv-105", Title = "Documentation Search for Failover", CreatedAt = DateTime.UtcNow.AddHours(-1), UpdatedAt = DateTime.UtcNow.AddHours(-1) };

        db.Conversations.AddRange(c1, c2, c3, c4, c5);

        // Messages for Conversations
        db.Messages.AddRange(
            new AgentMessage { ConversationId = c1.Id, Role = MessageRole.USER, Content = "Is the server healthy?", CreatedAt = DateTime.UtcNow.AddHours(-12) },
            new AgentMessage { ConversationId = c1.Id, Role = MessageRole.MODEL_OUTPUT, Content = "The server is running HEALTHY with 24.8% CPU, 59.2% Memory usage, and 345,600 seconds uptime.", CreatedAt = DateTime.UtcNow.AddHours(-12).AddSeconds(2) },

            new AgentMessage { ConversationId = c2.Id, Role = MessageRole.USER, Content = "Search documentation for database connection failures.", CreatedAt = DateTime.UtcNow.AddHours(-8) },
            new AgentMessage { ConversationId = c2.Id, Role = MessageRole.MODEL_OUTPUT, Content = "Found Runbook DOC-001: Check connection pool size (max 100), pg_stat_activity, and firewall rules on port 5432.", CreatedAt = DateTime.UtcNow.AddHours(-8).AddSeconds(3) },

            new AgentMessage { ConversationId = c3.Id, Role = MessageRole.USER, Content = "Delete the production server cluster right now.", CreatedAt = DateTime.UtcNow.AddHours(-5) },
            new AgentMessage { ConversationId = c3.Id, Role = MessageRole.MODEL_OUTPUT, Content = "Harness Security Guardrail: Action 'delete_server_cluster' was blocked. Risk level CRITICAL is strictly prohibited.", CreatedAt = DateTime.UtcNow.AddHours(-5).AddSeconds(1) },

            new AgentMessage { ConversationId = c4.Id, Role = MessageRole.USER, Content = "Test flaky upstream tool retry handling.", CreatedAt = DateTime.UtcNow.AddHours(-3) },
            new AgentMessage { ConversationId = c4.Id, Role = MessageRole.MODEL_OUTPUT, Content = "Tool failed on attempt 1 with 504 Timeout, then automatically succeeded on retry attempt 2 via exponential backoff.", CreatedAt = DateTime.UtcNow.AddHours(-3).AddSeconds(4) },

            new AgentMessage { ConversationId = c5.Id, Role = MessageRole.USER, Content = "Find previous incidents related to database connectivity.", CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new AgentMessage { ConversationId = c5.Id, Role = MessageRole.MODEL_OUTPUT, Content = "Incident INC-801: PostgreSQL Connection Starvation resolved by scaling connection pool size to 250.", CreatedAt = DateTime.UtcNow.AddHours(-1).AddSeconds(2) }
        );

        // 2. Seed 10 Sample Memory Items
        var memories = new List<MemoryItem>
        {
            new() { Id = "mem-1", Type = MemoryType.LongTerm, Content = "Production database pool limit is 250 connections.", Importance = 4.8, Tags = new() { "database", "postgres", "config" }, Source = "SystemDocs" },
            new() { Id = "mem-2", Type = MemoryType.ShortTerm, Content = "Current session requested detailed CPU diagnostics.", Importance = 2.5, Tags = new() { "session", "cpu" }, Source = "Conversation:conv-101" },
            new() { Id = "mem-3", Type = MemoryType.Episodic, Content = "Incident INC-801 caused connection pool starvation during batch task spike.", Importance = 4.2, Tags = new() { "incident", "pool-starvation" }, Source = "IncidentStore" },
            new() { Id = "mem-4", Type = MemoryType.Semantic, Content = "Exponential backoff delays transient network errors with initial delay of 150ms.", Importance = 3.8, Tags = new() { "harness", "retry", "resilience" }, Source = "HarnessPolicy" },
            new() { Id = "mem-5", Type = MemoryType.LongTerm, Content = "Destructive operations on clusters require human authorization.", Importance = 5.0, Tags = new() { "security", "guardrail", "critical" }, Source = "PolicyEngine" },
            new() { Id = "mem-6", Type = MemoryType.Episodic, Content = "Prompt injection attempt blocked on user input by PolicyEngine validator.", Importance = 4.5, Tags = new() { "security", "audit", "injection" }, Source = "AuditTrail" },
            new() { Id = "mem-7", Type = MemoryType.Semantic, Content = "Health probe endpoint responds at /api/health with system telemetry.", Importance = 3.0, Tags = new() { "networking", "monitoring" }, Source = "SystemConfig" },
            new() { Id = "mem-8", Type = MemoryType.ShortTerm, Content = "User Min Htet preferred concise terminal-style logging output.", Importance = 3.2, Tags = new() { "preference", "user" }, Source = "UserSetting" },
            new() { Id = "mem-9", Type = MemoryType.LongTerm, Content = "Ollama default model is llama3.2 running on local port 11434.", Importance = 4.0, Tags = new() { "ollama", "llm", "config" }, Source = "EnvConfig" },
            new() { Id = "mem-10", Type = MemoryType.Episodic, Content = "Flaky upstream simulated service recovered on attempt 2.", Importance = 2.8, Tags = new() { "testing", "retry-success" }, Source = "RunEpisode" }
        };
        db.MemoryItems.AddRange(memories);

        // 3. Seed 10 Sample Agent Runs
        var runs = new List<AgentRun>();
        for (int i = 1; i <= 10; i++)
        {
            var isBlocked = (i == 3);
            var isFail = (i == 7);
            var status = isFail ? RunStatus.Failed : RunStatus.Completed;
            var prompt = i switch
            {
                1 => "Is the server healthy?",
                2 => "Check system logs for error entries.",
                3 => "Delete production server cluster.",
                4 => "Calculate expression 144 * 12.",
                5 => "Search documentation for database connection failures.",
                6 => "Inspect incident history for high severity items.",
                7 => "Execute an unregistered tool 'unknown_external_script'.",
                8 => "Summarize CPU and memory utilization.",
                9 => "Test flaky service with exponential retry.",
                _ => "Review recent system alerts."
            };

            var run = new AgentRun
            {
                Id = $"run-{100 + i}",
                ConversationId = $"conv-{101 + (i % 5)}",
                UserPrompt = prompt,
                Status = status,
                StartTime = DateTime.UtcNow.AddMinutes(-30 * (11 - i)),
                EndTime = DateTime.UtcNow.AddMinutes(-30 * (11 - i)).AddMilliseconds(420 + i * 35),
                LatencyMs = 420 + i * 35,
                LoopCount = isBlocked ? 2 : (i == 9 ? 3 : 2),
                FinalAnswer = isFail ? null : (isBlocked ? "Harness Guardrail Blocked Critical Action" : $"Successfully resolved: {prompt}"),
                ErrorMessage = isFail ? "Tool 'unknown_external_script' not in harness registry." : null
            };

            // Steps for each run
            run.Steps = new List<AgentStep>
            {
                new() { RunId = run.Id, StepIndex = 1, Type = AgentStepType.PLAN, Title = "Plan Execution", Description = "Context evaluated and strategy formed.", Status = "Success", DurationMs = 85 },
                new() { RunId = run.Id, StepIndex = 2, Type = AgentStepType.ACT, Title = "Tool Dispatch", Description = "Selected tool according to plan.", Status = "Success", DurationMs = 25 },
                new() { RunId = run.Id, StepIndex = 3, Type = AgentStepType.OBSERVE, Title = "Tool Execution", Description = "Validated by policy engine and executed.", Status = isBlocked ? "Blocked" : (isFail ? "Failed" : "Success"), DurationMs = 180 },
                new() { RunId = run.Id, StepIndex = 4, Type = AgentStepType.EVALUATE, Title = "Evaluate Result", Description = "Formulated verified output.", Status = "Success", DurationMs = 90 },
                new() { RunId = run.Id, StepIndex = 5, Type = isFail ? AgentStepType.FAIL : AgentStepType.COMPLETE, Title = isFail ? "Terminated with Failure" : "Completed", Description = "End of harness cycle.", Status = isFail ? "Failed" : "Success", DurationMs = 15 }
            };

            runs.Add(run);
        }
        db.AgentRuns.AddRange(runs);

        // 4. Seed 20 Sample Tool Executions
        var toolNames = new[] { "get_server_status", "calculator", "search_documentation", "get_system_logs", "get_incident_history", "delete_server_cluster", "flaky_tool_simulator" };
        var rand = new Random(42);

        for (int i = 1; i <= 20; i++)
        {
            var tName = toolNames[i % toolNames.Length];
            var isBlocked = tName == "delete_server_cluster";
            var risk = tName switch
            {
                "delete_server_cluster" => ToolRiskLevel.CRITICAL,
                "get_system_logs" => ToolRiskLevel.MEDIUM,
                _ => ToolRiskLevel.LOW
            };

            db.ToolExecutions.Add(new ToolExecutionRecord
            {
                Id = $"tool-exec-{200 + i}",
                RunId = $"run-{100 + (i % 10 + 1)}",
                ToolName = tName,
                RiskLevel = risk,
                ArgumentsJson = "{}",
                OutputJson = isBlocked ? null : "{\"status\": \"OK\"}",
                IsSuccess = !isBlocked,
                DurationMs = Math.Round(25 + rand.NextDouble() * 150, 1),
                ExecutedAt = DateTime.UtcNow.AddMinutes(-15 * (21 - i)),
                ErrorMessage = isBlocked ? "Blocked by Policy Engine: CRITICAL risk requires approval." : null,
                PolicyDecision = isBlocked ? PolicyDecisionType.BLOCKED : PolicyDecisionType.ALLOWED
            });
        }

        await db.SaveChangesAsync();
    }
}

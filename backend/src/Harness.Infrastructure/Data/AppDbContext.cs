using Harness.Models;
using Microsoft.EntityFrameworkCore;

namespace Harness.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<AgentMessage> Messages => Set<AgentMessage>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    public DbSet<ToolExecutionRecord> ToolExecutions => Set<ToolExecutionRecord>();
    public DbSet<MemoryItem> MemoryItems => Set<MemoryItem>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Conversation
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasMany(c => c.Messages)
                  .WithOne()
                  .HasForeignKey(m => m.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AgentMessage
        modelBuilder.Entity<AgentMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Metadata)
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
        });

        // AgentRun
        modelBuilder.Entity<AgentRun>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasMany(r => r.Steps)
                  .WithOne()
                  .HasForeignKey(s => s.RunId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AgentStep
        modelBuilder.Entity<AgentStep>(entity =>
        {
            entity.HasKey(s => s.Id);
        });

        // MemoryItem
        modelBuilder.Entity<MemoryItem>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Tags)
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());

            entity.Property(m => m.Embedding)
                  .HasConversion(
                      v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<float[]>(v, (System.Text.Json.JsonSerializerOptions?)null));
        });

        // ToolExecutionRecord
        modelBuilder.Entity<ToolExecutionRecord>(entity =>
        {
            entity.HasKey(t => t.Id);
        });
    }
}

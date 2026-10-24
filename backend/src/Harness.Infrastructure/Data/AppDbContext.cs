using Harness.Core.Interfaces;
using Harness.Models;
using Microsoft.EntityFrameworkCore;

namespace Harness.Infrastructure.Data;

public class DbToolExecutionStore : IToolExecutionStore
{
    private readonly AppDbContext _db;

    public DbToolExecutionStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task RecordExecutionAsync(ToolExecutionRecord record, CancellationToken cancellationToken = default)
    {
        _db.ToolExecutions.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public class DbAgentRunStore : IAgentRunStore
{
    private readonly AppDbContext _db;

    public DbAgentRunStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task CreateRunAsync(AgentRun run, CancellationToken cancellationToken = default)
    {
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRunAsync(AgentRun run, CancellationToken cancellationToken = default)
    {
        var existing = await _db.AgentRuns.Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == run.Id, cancellationToken);
        if (existing != null)
        {
            existing.Status = run.Status;
            existing.EndTime = run.EndTime;
            existing.LatencyMs = run.LatencyMs;
            existing.LoopCount = run.LoopCount;
            existing.FinalAnswer = run.FinalAnswer;
            existing.ErrorMessage = run.ErrorMessage;
            existing.Steps = run.Steps;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

public class DbMemoryStore : IMemoryStore
{
    private readonly AppDbContext _db;

    public DbMemoryStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MemoryItem> SaveAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        var existing = await _db.MemoryItems.FirstOrDefaultAsync(m => m.Id == item.Id, cancellationToken);
        if (existing != null)
        {
            existing.Content = item.Content;
            existing.Type = item.Type;
            existing.Importance = item.Importance;
            existing.Tags = item.Tags;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Source = item.Source;
        }
        else
        {
            _db.MemoryItems.Add(item);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.MemoryItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (existing == null) return false;

        _db.MemoryItems.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<MemoryItem>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.MemoryItems.AsNoTracking().ToListAsync(cancellationToken);
    }
}

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
            entity.Ignore(m => m.ToolCalls);
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

using FluentAssertions;
using Harness.Memory;
using Harness.Models;
using Xunit;

namespace Harness.Core.Tests;

public class MemoryServiceTests
{
    [Fact]
    public async Task AddAndGetAllMemories_ShouldStoreAndRetrieveItems()
    {
        var service = new MemoryService();
        var item = new MemoryItem
        {
            Type = MemoryType.LongTerm,
            Content = "Test memory note",
            Importance = 4.0,
            Tags = new List<string> { "test", "security" }
        };

        var added = await service.AddMemoryAsync(item);
        added.Id.Should().NotBeNullOrEmpty();

        var all = await service.GetAllMemoriesAsync();
        all.Should().ContainSingle(m => m.Content == "Test memory note");
    }

    [Fact]
    public async Task SearchMemory_ShouldReturnRelevantAndExcludeIrrelevant()
    {
        var service = new MemoryService(new[]
        {
            new MemoryItem { Id = "1", Content = "Database connection pool timeout settings", Importance = 4.5, Tags = new() { "database" } },
            new MemoryItem { Id = "2", Content = "Frontend UI color scheme configuration", Importance = 1.0, Tags = new() { "ui" } }
        });

        var results = await service.SearchMemoryAsync("database pool error");

        results.Should().NotBeEmpty();
        results[0].Id.Should().Be("1");
        results.Should().NotContain(m => m.Id == "2");
    }

    [Fact]
    public async Task DeleteMemory_ShouldRemoveItem()
    {
        var service = new MemoryService();
        var item = await service.AddMemoryAsync(new MemoryItem { Content = "Temporary note" });

        var deleted = await service.DeleteMemoryAsync(item.Id);
        deleted.Should().BeTrue();

        var all = await service.GetAllMemoriesAsync();
        all.Should().BeEmpty();
    }
}

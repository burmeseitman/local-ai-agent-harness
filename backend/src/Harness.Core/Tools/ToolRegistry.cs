using System.Collections.Concurrent;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Core.Tools;

public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ToolRegistry(IEnumerable<ITool>? initialTools = null)
    {
        if (initialTools != null)
        {
            foreach (var tool in initialTools)
            {
                RegisterTool(tool);
            }
        }
    }

    public void RegisterTool(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools[tool.Name] = tool;
    }

    public ITool? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IReadOnlyList<ITool> GetAllTools() => _tools.Values.ToList();

    public bool HasTool(string name) => !string.IsNullOrWhiteSpace(name) && _tools.ContainsKey(name);

    public List<ToolDefinition> GetDefinitions()
    {
        return _tools.Values.Select(t => new ToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            RiskLevel = t.RiskLevel,
            InputSchema = t.InputSchema,
            OutputSchema = t.OutputSchema
        }).ToList();
    }
}

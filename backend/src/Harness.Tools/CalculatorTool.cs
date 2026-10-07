using System.Diagnostics;
using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Tools;

public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "Performs safe arithmetic calculations: addition, subtraction, multiplication, division.";
    public ToolRiskLevel RiskLevel => ToolRiskLevel.LOW;

    public JsonDocument InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["operation", "a", "b"],
        "properties": {
            "operation": {
                "type": "string",
                "enum": ["add", "subtract", "multiply", "divide"],
                "description": "Arithmetic operation to perform"
            },
            "a": { "type": "number", "description": "First operand" },
            "b": { "type": "number", "description": "Second operand" }
        }
    }
    """);

    public JsonDocument OutputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "required": ["result", "operation", "expression"],
        "properties": {
            "result": { "type": "number" },
            "operation": { "type": "string" },
            "expression": { "type": "string" }
        }
    }
    """);

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var root = doc.RootElement;

            string op = root.TryGetProperty("operation", out var opProp) ? opProp.GetString()?.ToLowerInvariant() ?? "add" : "add";
            double a = root.TryGetProperty("a", out var aProp) && aProp.TryGetDouble(out var aVal) ? aVal : 0;
            double b = root.TryGetProperty("b", out var bProp) && bProp.TryGetDouble(out var bVal) ? bVal : 0;

            double result = 0;
            string expr = "";

            switch (op)
            {
                case "add":
                    result = a + b;
                    expr = $"{a} + {b} = {result}";
                    break;
                case "subtract":
                    result = a - b;
                    expr = $"{a} - {b} = {result}";
                    break;
                case "multiply":
                    result = a * b;
                    expr = $"{a} * {b} = {result}";
                    break;
                case "divide":
                    if (Math.Abs(b) < double.Epsilon)
                    {
                        return Task.FromResult(new ToolResult
                        {
                            ToolName = Name,
                            IsSuccess = false,
                            ErrorMessage = "Division by zero is undefined.",
                            ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
                            RiskLevel = RiskLevel,
                            Status = ToolExecutionStatus.Failed
                        });
                    }
                    result = a / b;
                    expr = $"{a} / {b} = {result}";
                    break;
                default:
                    return Task.FromResult(new ToolResult
                    {
                        ToolName = Name,
                        IsSuccess = false,
                        ErrorMessage = $"Unsupported operation: {op}. Supported: add, subtract, multiply, divide.",
                        ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
                        RiskLevel = RiskLevel,
                        Status = ToolExecutionStatus.Failed
                    });
            }

            sw.Stop();
            var payload = new
            {
                result,
                operation = op,
                expression = expr
            };

            return Task.FromResult(new ToolResult
            {
                ToolName = Name,
                IsSuccess = true,
                OutputJson = JsonSerializer.Serialize(payload),
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
                RiskLevel = RiskLevel,
                Status = ToolExecutionStatus.Success
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Task.FromResult(new ToolResult
            {
                ToolName = Name,
                IsSuccess = false,
                ErrorMessage = $"Argument parsing error: {ex.Message}",
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds,
                RiskLevel = RiskLevel,
                Status = ToolExecutionStatus.Failed
            });
        }
    }
}

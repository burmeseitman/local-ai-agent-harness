using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Core.Validation;

public class OutputValidatorOptions
{
    public int MaxResultSizeBytes { get; set; } = 65536; // 64 KB
}

public class OutputValidator : IOutputValidator
{
    private readonly OutputValidatorOptions _options;

    public OutputValidator(OutputValidatorOptions? options = null)
    {
        _options = options ?? new OutputValidatorOptions();
    }

    public ValidationResult Validate(ITool tool, ToolResult result)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(result);

        // 1. If result failed already
        if (!result.IsSuccess)
        {
            var err = result.ErrorMessage ?? "Tool execution failed";
            var isRetryable = err.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
                              err.Contains("504", StringComparison.OrdinalIgnoreCase) ||
                              err.Contains("503", StringComparison.OrdinalIgnoreCase) ||
                              err.Contains("transient", StringComparison.OrdinalIgnoreCase) ||
                              err.Contains("connection reset", StringComparison.OrdinalIgnoreCase);

            var outcome = isRetryable ? ValidationOutcome.Retryable : ValidationOutcome.Fatal;
            return ValidationResult.Fail(outcome, err);
        }

        // 2. Output size check
        if (string.IsNullOrEmpty(result.OutputJson))
        {
            return ValidationResult.Fail(ValidationOutcome.Invalid, "Tool returned empty output JSON.");
        }

        if (result.OutputJson.Length > _options.MaxResultSizeBytes)
        {
            return ValidationResult.Fail(
                ValidationOutcome.Fatal,
                $"Tool output exceeded maximum size limit of {_options.MaxResultSizeBytes} bytes (got {result.OutputJson.Length} bytes).");
        }

        // 3. JSON well-formedness
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(result.OutputJson);
        }
        catch (JsonException ex)
        {
            return ValidationResult.Fail(ValidationOutcome.Invalid, $"Malformed JSON output from tool: {ex.Message}");
        }

        using (doc)
        {
            // 4. Schema required fields validation
            if (tool.OutputSchema != null)
            {
                var schemaRoot = tool.OutputSchema.RootElement;
                if (schemaRoot.TryGetProperty("required", out var reqArray) && reqArray.ValueKind == JsonValueKind.Array)
                {
                    var outputRoot = doc.RootElement;
                    if (outputRoot.ValueKind != JsonValueKind.Object)
                    {
                        return ValidationResult.Fail(ValidationOutcome.Invalid, "Tool output root must be a JSON object.");
                    }

                    var missingFields = new List<string>();
                    foreach (var req in reqArray.EnumerateArray())
                    {
                        var fieldName = req.GetString();
                        if (!string.IsNullOrEmpty(fieldName) && !outputRoot.TryGetProperty(fieldName, out _))
                        {
                            missingFields.Add(fieldName);
                        }
                    }

                    if (missingFields.Count > 0)
                    {
                        return ValidationResult.Fail(
                            ValidationOutcome.Invalid,
                            $"Output schema validation failed: missing required fields [{string.Join(", ", missingFields)}].");
                    }
                }
            }
        }

        return ValidationResult.Success();
    }
}

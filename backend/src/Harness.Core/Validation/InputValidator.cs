using System.Text.Json;
using Harness.Core.Interfaces;
using Harness.Models;

namespace Harness.Core.Validation;

public class InputValidator : IInputValidator
{
    public ValidationResult Validate(ITool tool, string argumentsJson)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (tool.InputSchema == null)
        {
            return ValidationResult.Success();
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException ex)
        {
            return ValidationResult.Fail(ValidationOutcome.Invalid, $"Malformed JSON input arguments: {ex.Message}");
        }

        using (doc)
        {
            var schemaRoot = tool.InputSchema.RootElement;
            var argsRoot = doc.RootElement;

            if (argsRoot.ValueKind != JsonValueKind.Object)
            {
                return ValidationResult.Fail(ValidationOutcome.Invalid, "Tool arguments root must be a JSON object.");
            }

            // Check required fields
            if (schemaRoot.TryGetProperty("required", out var reqArray) && reqArray.ValueKind == JsonValueKind.Array)
            {
                var missingFields = new List<string>();
                foreach (var req in reqArray.EnumerateArray())
                {
                    var fieldName = req.GetString();
                    if (!string.IsNullOrEmpty(fieldName) && !argsRoot.TryGetProperty(fieldName, out _))
                    {
                        missingFields.Add(fieldName);
                    }
                }

                if (missingFields.Count > 0)
                {
                    return ValidationResult.Fail(
                        ValidationOutcome.Invalid,
                        $"Input schema validation failed: missing required parameter(s) [{string.Join(", ", missingFields)}].");
                }
            }

            // Check property types and enums if specified
            if (schemaRoot.TryGetProperty("properties", out var propsObj) && propsObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in propsObj.EnumerateObject())
                {
                    if (argsRoot.TryGetProperty(prop.Name, out var actualVal))
                    {
                        var propDef = prop.Value;
                        if (propDef.TryGetProperty("type", out var typeProp))
                        {
                            var expectedType = typeProp.GetString();
                            bool matches = expectedType switch
                            {
                                "string" => actualVal.ValueKind == JsonValueKind.String,
                                "number" => actualVal.ValueKind == JsonValueKind.Number,
                                "integer" => actualVal.ValueKind == JsonValueKind.Number && actualVal.TryGetInt64(out _),
                                "boolean" => actualVal.ValueKind is JsonValueKind.True or JsonValueKind.False,
                                "array" => actualVal.ValueKind == JsonValueKind.Array,
                                "object" => actualVal.ValueKind == JsonValueKind.Object,
                                _ => true
                            };

                            if (!matches)
                            {
                                return ValidationResult.Fail(
                                    ValidationOutcome.Invalid,
                                    $"Input schema validation failed for parameter '{prop.Name}': expected type '{expectedType}', got '{actualVal.ValueKind}'.");
                            }
                        }

                        // Check enum values
                        if (propDef.TryGetProperty("enum", out var enumProp) && enumProp.ValueKind == JsonValueKind.Array)
                        {
                            var actualStr = actualVal.GetString();
                            var allowed = enumProp.EnumerateArray().Select(e => e.GetString()).ToList();
                            if (actualStr == null || !allowed.Contains(actualStr, StringComparer.OrdinalIgnoreCase))
                            {
                                return ValidationResult.Fail(
                                    ValidationOutcome.Invalid,
                                    $"Input schema validation failed for parameter '{prop.Name}': value '{actualStr}' is not in allowed values [{string.Join(", ", allowed)}].");
                            }
                        }
                    }
                }
            }
        }

        return ValidationResult.Success();
    }
}

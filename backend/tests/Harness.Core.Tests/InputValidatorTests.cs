using System.Text.Json;
using FluentAssertions;
using Harness.Core.Interfaces;
using Harness.Core.Validation;
using Harness.Models;
using NSubstitute;
using Xunit;

namespace Harness.Core.Tests;

public class InputValidatorTests
{
    private readonly InputValidator _validator = new();

    private ITool CreateToolWithInputSchema(string inputSchemaJson)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("calc_tool");
        tool.InputSchema.Returns(JsonDocument.Parse(inputSchemaJson));
        return tool;
    }

    [Fact]
    public void Validate_WhenArgumentsMatchSchema_ShouldPass()
    {
        var tool = CreateToolWithInputSchema("""
        {
            "type": "object",
            "required": ["operation", "a", "b"],
            "properties": {
                "operation": { "type": "string", "enum": ["add", "subtract"] },
                "a": { "type": "number" },
                "b": { "type": "number" }
            }
        }
        """);

        var validArgs = "{\"operation\": \"add\", \"a\": 10, \"b\": 20}";
        var result = _validator.Validate(tool, validArgs);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenMissingRequiredProperty_ShouldFail()
    {
        var tool = CreateToolWithInputSchema("""
        {
            "type": "object",
            "required": ["operation", "a", "b"]
        }
        """);

        var missingArgs = "{\"operation\": \"add\", \"a\": 10}";
        var result = _validator.Validate(tool, missingArgs);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("b");
    }

    [Fact]
    public void Validate_WhenPropertyTypeMismatch_ShouldFail()
    {
        var tool = CreateToolWithInputSchema("""
        {
            "type": "object",
            "properties": {
                "a": { "type": "number" }
            }
        }
        """);

        var invalidTypeArgs = "{\"a\": \"not_a_number\"}";
        var result = _validator.Validate(tool, invalidTypeArgs);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("expected type 'number'");
    }

    [Fact]
    public void Validate_WhenEnumValueNotInAllowedList_ShouldFail()
    {
        var tool = CreateToolWithInputSchema("""
        {
            "type": "object",
            "properties": {
                "operation": { "type": "string", "enum": ["add", "multiply"] }
            }
        }
        """);

        var invalidEnum = "{\"operation\": \"destroy\"}";
        var result = _validator.Validate(tool, invalidEnum);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("destroy");
    }
}

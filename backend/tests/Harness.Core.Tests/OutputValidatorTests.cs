using System.Text.Json;
using FluentAssertions;
using Harness.Core.Interfaces;
using Harness.Core.Validation;
using Harness.Models;
using NSubstitute;
using Xunit;

namespace Harness.Core.Tests;

public class OutputValidatorTests
{
    private readonly OutputValidator _validator = new(new OutputValidatorOptions { MaxResultSizeBytes = 1000 });

    private ITool CreateToolWithSchema(string outputSchemaJson)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("test_tool");
        tool.OutputSchema.Returns(JsonDocument.Parse(outputSchemaJson));
        return tool;
    }

    [Fact]
    public void Validate_WhenOutputMatchesSchema_ShouldReturnValid()
    {
        var tool = CreateToolWithSchema("""
        {
            "type": "object",
            "required": ["status", "value"]
        }
        """);

        var result = new ToolResult
        {
            IsSuccess = true,
            OutputJson = "{\"status\": \"OK\", \"value\": 42}"
        };

        var validation = _validator.Validate(tool, result);

        validation.IsValid.Should().BeTrue();
        validation.Outcome.Should().Be(ValidationOutcome.Valid);
    }

    [Fact]
    public void Validate_WhenMissingRequiredField_ShouldReturnInvalid()
    {
        var tool = CreateToolWithSchema("""
        {
            "type": "object",
            "required": ["status", "missingProp"]
        }
        """);

        var result = new ToolResult
        {
            IsSuccess = true,
            OutputJson = "{\"status\": \"OK\"}"
        };

        var validation = _validator.Validate(tool, result);

        validation.IsValid.Should().BeFalse();
        validation.Outcome.Should().Be(ValidationOutcome.Invalid);
        validation.Message.Should().Contain("missingProp");
    }

    [Fact]
    public void Validate_WhenOutputExceedsMaxSize_ShouldReturnFatal()
    {
        var tool = CreateToolWithSchema("{}");
        var result = new ToolResult
        {
            IsSuccess = true,
            OutputJson = "{\"data\": \"" + new string('A', 1200) + "\"}" // Exceeds 1000 limit
        };

        var validation = _validator.Validate(tool, result);

        validation.IsValid.Should().BeFalse();
        validation.Outcome.Should().Be(ValidationOutcome.Fatal);
    }

    [Fact]
    public void Validate_WhenTransientFailure_ShouldReturnRetryable()
    {
        var tool = CreateToolWithSchema("{}");
        var result = new ToolResult
        {
            IsSuccess = false,
            ErrorMessage = "504 Gateway Timeout during request execution"
        };

        var validation = _validator.Validate(tool, result);

        validation.Outcome.Should().Be(ValidationOutcome.Retryable);
    }
}

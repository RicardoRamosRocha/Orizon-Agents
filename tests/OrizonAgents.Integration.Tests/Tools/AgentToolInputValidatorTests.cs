using System.Text.Json;
using OrizonAgents.Infrastructure.Tools.Validation;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class AgentToolInputValidatorTests
{
    private readonly AgentToolInputValidator _validator = new();

    private const string Schema = """
    {
      "type": "object",
      "properties": {
        "date": {
          "type": "string",
          "format": "date"
        },
        "startsAt": {
          "type": "string"
        },
        "endsAt": {
          "type": "string"
        }
      },
      "required": ["date", "startsAt", "endsAt"],
      "additionalProperties": false
    }
    """;

    [Fact]
    public void Valid_input_should_pass()
    {
        JsonElement input = Parse("""
        {
          "date": "2026-09-05",
          "startsAt": "08:00:00",
          "endsAt": "12:00:00"
        }
        """);

        var result = _validator.Validate(Schema, input);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_required_property_should_fail()
    {
        JsonElement input = Parse("""
        {
          "date": "2026-09-05",
          "startsAt": "08:00:00"
        }
        """);

        var result = _validator.Validate(Schema, input);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("endsAt"));
    }

    [Fact]
    public void Additional_property_should_fail()
    {
        JsonElement input = Parse("""
        {
          "date": "2026-09-05",
          "startsAt": "08:00:00",
          "endsAt": "12:00:00",
          "dangerousField": "unexpected"
        }
        """);

        var result = _validator.Validate(Schema, input);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("dangerousField"));
    }

    [Fact]
    public void Wrong_property_type_should_fail()
    {
        JsonElement input = Parse("""
        {
          "date": 123,
          "startsAt": "08:00:00",
          "endsAt": "12:00:00"
        }
        """);

        var result = _validator.Validate(Schema, input);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("$.date"));
    }

    [Fact]
    public void Invalid_date_format_should_fail()
    {
        JsonElement input = Parse("""
        {
          "date": "05/09/2026",
          "startsAt": "08:00:00",
          "endsAt": "12:00:00"
        }
        """);

        var result = _validator.Validate(Schema, input);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("yyyy-MM-dd"));
    }

    [Fact]
    public void Invalid_schema_should_fail_closed()
    {
        JsonElement input = Parse("""
        {
          "date": "2026-09-05"
        }
        """);

        var result = _validator.Validate(
            "{ invalid json",
            input);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_schema_should_allow_input()
    {
        JsonElement input = Parse("""
        {
          "anything": "value"
        }
        """);

        var result = _validator.Validate(null, input);

        Assert.True(result.IsValid);
    }

    private static JsonElement Parse(string json)
    {
        using JsonDocument document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }
}

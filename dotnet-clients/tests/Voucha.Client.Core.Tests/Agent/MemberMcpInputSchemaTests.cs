using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpInputSchemaTests
{
  [Fact]
  public void CurrentProducerCatalogProjectsEveryAdvertisedTool()
  {
    using var fixture = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp.json")));
    var server = fixture.RootElement.GetProperty("servers").EnumerateArray()
        .Single(item => item.GetProperty("name").GetString() == "voucha-user-mcp");
    var tools = server.GetProperty("tools").EnumerateArray()
        .Select(item => item.GetProperty("tool")).ToArray();
    using var catalog = JsonDocument.Parse(JsonSerializer.Serialize(new { tools }));

    var projected = new MemberMcpToolBridge(catalog.RootElement);

    Assert.NotEmpty(projected.Tools);
    Assert.Equal(tools.Length, projected.Tools.Count);
  }

  [Fact]
  public void LocalReferenceAndSiblingConstraintsBothApply()
  {
    using var schema = JsonDocument.Parse("""{"type":"object","properties":{"name":{"$ref":"#/$defs/ShortText","maxLength":3}},"required":["name"],"$defs":{"ShortText":{"type":"string","minLength":2}}}""");

    Assert.True(Accepts("""{"name":"Ada"}""", schema));
    Assert.False(Accepts("""{"name":"A"}""", schema));
    Assert.False(Accepts("""{"name":"Adana"}""", schema));
  }

  [Fact]
  public void SelectedOptionAndNestedArgumentsMustMatchSameBranch()
  {
    using var schema = JsonDocument.Parse("""{"type":"object","oneOf":[{"type":"object","properties":{"option":{"enum":["details"]},"arguments":{"type":"object","required":["id"],"properties":{"id":{"type":"string"}},"additionalProperties":false}},"required":["option","arguments"],"additionalProperties":false},{"type":"object","properties":{"option":{"enum":["list"]},"arguments":{"type":"object","properties":{"after":{"type":"string"}},"additionalProperties":false}},"required":["option","arguments"],"additionalProperties":false}]}""");

    Assert.True(Accepts("""{"option":"details","arguments":{"id":"abc"}}""", schema));
    Assert.False(Accepts("""{"option":"details","arguments":{"after":"cursor"}}""", schema));
    Assert.False(Accepts("""{"option":"list","arguments":{"id":"abc"}}""", schema));
    Assert.False(Accepts("""{"option":"unknown","arguments":{}}""", schema));
  }

  [Fact]
  public void DecimalBoundsRejectLargeIntegerAboveExactMaximum()
  {
    using var schema = JsonDocument.Parse("""{"type":"integer","maximum":9007199254740992}""");

    Assert.True(Accepts("9007199254740992", schema));
    Assert.False(Accepts("9007199254740993", schema));
  }

  [Fact]
  public void DecimalUnderflowCannotSatisfyIntegerConstOrZeroMaximum()
  {
    using var integer = JsonDocument.Parse("""{"type":"integer"}""");
    using var number = JsonDocument.Parse("""{"type":"number"}""");
    using var constant = JsonDocument.Parse("""{"const":0}""");
    using var maximum = JsonDocument.Parse("""{"maximum":0}""");
    using var underflowedMaximum = JsonDocument.Parse("""{"maximum":1e-100}""");

    Assert.False(Accepts("1e-100", integer));
    Assert.False(Accepts("1e-100", number));
    Assert.False(Accepts("1e-100", constant));
    Assert.False(Accepts("1e-100", maximum));
    Assert.Throws<InvalidDataException>(() => Accepts("1", underflowedMaximum));
    Assert.False(Accepts("1.00000000000000000000000000001", integer));
    Assert.False(Accepts("0.00000000000000000000000000001", maximum));
    Assert.True(Accepts("0e-100", integer));
    Assert.True(Accepts("0", constant));
    Assert.True(Accepts("0", maximum));
  }

  [Fact]
  public void AdditionalPropertiesDefaultsToAllowedButExplicitFalseRejectsThem()
  {
    using var defaultSchema = JsonDocument.Parse("""{"type":"object","properties":{"id":{"type":"string"}}}""");
    using var closedSchema = JsonDocument.Parse("""{"type":"object","properties":{"id":{"type":"string"}},"additionalProperties":false}""");

    Assert.True(Accepts("""{"id":"a","extra":1}""", defaultSchema));
    Assert.False(Accepts("""{"id":"a","extra":1}""", closedSchema));
  }

  [Fact]
  public void UnsupportedAdmissionKeywordFailsCatalogBeforeModelRequest()
  {
    using var catalog = JsonDocument.Parse("""{"tools":[{"name":"unsafe","inputSchema":{"type":"object","dependentRequired":{"key":["other"]}}}]}""");

    Assert.Throws<InvalidDataException>(() => new MemberMcpToolBridge(catalog.RootElement));
  }

  [Fact]
  public void UnsupportedNonBacktrackingPatternFailsCatalogBeforeModelRequest()
  {
    using var catalog = JsonDocument.Parse("""{"tools":[{"name":"unsafe","inputSchema":{"type":"object","properties":{"name":{"type":"string","pattern":"^(?=a)"}}}}]}""");

    Assert.Throws<InvalidDataException>(() => new MemberMcpToolBridge(catalog.RootElement));
  }

  [Fact]
  public void PatternUsesEcmaCharacterClassesBeforeToolAdmission()
  {
    using var digits = JsonDocument.Parse("""{"type":"string","pattern":"^\\d+$"}""");
    using var nonWhitespace = JsonDocument.Parse("""{"type":"string","pattern":"^\\S+$"}""");
    using var whitespace = JsonDocument.Parse("""{"type":"string","pattern":"^\\s$"}""");
    using var dot = JsonDocument.Parse("""{"type":"string","pattern":"^.$"}""");
    using var nonCapturingGroup = JsonDocument.Parse("""{"type":"string","pattern":"^(?:a|b)$"}""");

    Assert.True(Accepts("\"123\"", digits));
    Assert.False(Accepts("\"١٢٣\"", digits));
    Assert.False(Accepts("\"123\\n\"", digits));
    Assert.True(Accepts("\"\\u0085\"", nonWhitespace));
    Assert.False(Accepts("\"\\u00A0\"", nonWhitespace));
    Assert.True(Accepts("\"\\u00A0\"", whitespace));
    Assert.False(Accepts("\"\\u2028\"", dot));
    Assert.True(Accepts("\"a\"", nonCapturingGroup));
  }

  [Fact]
  public void UnsupportedEcmaBoundaryAndClassEscapesFailCatalogAdmission()
  {
    foreach (var pattern in new[] { "\\bword\\b", "[\\d]", "[a-[b]]", "\\A", "\\Z", "\\x1", "\\u123",
                 "(?i)^admin$", "(?x)admin", "(?#comment)admin" })
    {
      using var schema = JsonDocument.Parse(JsonSerializer.Serialize(new { type = "string", pattern }));
      Assert.Throws<InvalidDataException>(() => MemberMcpInputSchema.Inspect(schema.RootElement));
    }
  }

  [Fact]
  public void UnsupportedModelFunctionNameFailsCatalogBeforeModelRequest()
  {
    using var catalog = JsonDocument.Parse("""{"tools":[{"name":"unsafe.tool","inputSchema":{"type":"object"}}]}""");
    Assert.Throws<InvalidDataException>(() => new MemberMcpToolBridge(catalog.RootElement));
  }

  [Fact]
  public void UriFormatRejectsHostNormalizationAndInvalidPercentEscapes()
  {
    using var schema = JsonDocument.Parse("""{"type":"string","format":"uri"}""");
    Assert.True(Accepts("\"https://example.com/a%20b\"", schema));
    Assert.False(Accepts("\"https://example.com/a b\"", schema));
    Assert.False(Accepts("\"https://example.com/%zz\"", schema));
  }

  [Fact]
  public void UuidFormatRequiresCanonicalHyphenatedRepresentation()
  {
    using var schema = JsonDocument.Parse("""{"type":"string","format":"uuid"}""");
    Assert.True(Accepts("\"123e4567-e89b-12d3-a456-426614174000\"", schema));
    Assert.True(Accepts("\"123E4567-E89B-12D3-A456-426614174000\"", schema));
    Assert.False(Accepts("\" 123e4567-e89b-12d3-a456-426614174000 \"", schema));
    Assert.False(Accepts("\"123e4567e89b12d3a456426614174000\"", schema));
    Assert.False(Accepts("\"{123e4567-e89b-12d3-a456-426614174000}\"", schema));
    Assert.False(Accepts("\"urn:uuid:123e4567-e89b-12d3-a456-426614174000\"", schema));
  }

  [Fact]
  public void ProducerBoundedHtmlUnicodeAndSlashesSurviveNativeResultEnvelope()
  {
    var text = string.Concat(Enumerable.Repeat("<é/\u0080\u2028", 100_000));
    using var producer = JsonDocument.Parse(
        "{\"content\":[{\"type\":\"text\",\"text\":\"" + text + "\"}]}");
    var producerBytes = System.Text.Encoding.UTF8.GetByteCount(producer.RootElement.GetRawText());
    Assert.InRange(producerBytes, 256_001, 1_048_576);
    var result = new McpToolResult(producer.RootElement.GetProperty("content").Clone(), null, false);

    var envelope = MemberMcpToolBridge.SerializeResultEnvelope(result);

    Assert.InRange(System.Text.Encoding.UTF8.GetByteCount(envelope), 256_001,
        MemberMcpToolBridge.MaximumEnvelopeBytes);
    using var decoded = JsonDocument.Parse(envelope);
    Assert.Equal(text, decoded.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
  }

  [Fact]
  public void DuplicateObjectMembersCannotSatisfyPropertyCountsOrOverrideValidatedValues()
  {
    using var schema = JsonDocument.Parse("""{"type":"object","properties":{"payload":{"type":"object","minProperties":2,"properties":{"id":{"type":"string","maxLength":3}}}}}""");
    Assert.True(Accepts("""{"payload":{"id":"one","other":1}}""", schema));
    Assert.False(Accepts("""{"payload":{"id":"one","id":"two"}}""", schema));
    Assert.False(Accepts("""{"payload":{"id":"one","id":"too-long"}}""", schema));
  }

  [Fact]
  public void UniqueItemsRequiresAnExplicitLocallyBoundedSchema()
  {
    foreach (var schema in new[]
    {
      """{"type":"array","uniqueItems":true}""",
      """{"type":"array","uniqueItems":true,"maxItems":257}""",
    })
    {
      using var document = JsonDocument.Parse(schema);
      Assert.Throws<InvalidDataException>(() => MemberMcpInputSchema.Inspect(document.RootElement));
    }
    using var bounded = JsonDocument.Parse("""{"type":"array","uniqueItems":true,"maxItems":256}""");
    using var accepted = JsonDocument.Parse(JsonSerializer.Serialize(Enumerable.Range(0, 256)));
    using var rejected = JsonDocument.Parse(JsonSerializer.Serialize(Enumerable.Range(0, 257)));
    Assert.True(MemberMcpInputSchema.Accepts(accepted.RootElement, bounded.RootElement));
    Assert.False(MemberMcpInputSchema.Accepts(rejected.RootElement, bounded.RootElement));
  }

  [Fact]
  public void ComposedReferenceDagHasACumulativeValidationWorkBound()
  {
    var definitions = new Dictionary<string, object> { ["leaf"] = new { type = "object" } };
    for (var level = 6; level >= 0; level--)
    {
      var next = level == 6 ? "leaf" : $"level{level + 1}";
      definitions[$"level{level}"] = new
      {
        allOf = Enumerable.Range(0, 4)
            .Select(_ => new Dictionary<string, string> { ["$ref"] = $"#/$defs/{next}" }).ToArray(),
      };
    }
    using var schema = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object>
    {
      ["$defs"] = definitions,
      ["$ref"] = "#/$defs/level0",
    }));
    using var value = JsonDocument.Parse("{} ");
    Assert.Throws<InvalidDataException>(() => MemberMcpInputSchema.Accepts(value.RootElement, schema.RootElement));

    using var shortSchema = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object>
    {
      ["$defs"] = definitions,
      ["allOf"] = Enumerable.Range(0, 2).Select(_ => new Dictionary<string, string>
      {
        ["$ref"] = "#/$defs/leaf",
      }).ToArray(),
    }));
    Assert.True(MemberMcpInputSchema.Accepts(value.RootElement, shortSchema.RootElement));
  }

  private static bool Accepts(string rawValue, JsonDocument schema)
  {
    using var value = JsonDocument.Parse(rawValue);
    return MemberMcpInputSchema.Accepts(value.RootElement, schema.RootElement);
  }
}

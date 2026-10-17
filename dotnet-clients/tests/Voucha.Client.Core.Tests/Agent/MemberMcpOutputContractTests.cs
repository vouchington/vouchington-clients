using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpOutputContractTests
{
  internal const string MappedTool = """
      {"name":"edit_profile","inputSchema":{"type":"object","oneOf":[
        {"type":"object","properties":{"option":{"enum":["bio"]},"arguments":{"type":"object"}},"required":["option","arguments"],"additionalProperties":false},
        {"type":"object","properties":{"option":{"enum":["links"]},"arguments":{"type":"object"}},"required":["option","arguments"],"additionalProperties":false}]},
       "outputSchema":{"type":"object","anyOf":[
        {"type":"object","properties":{"bio":{"type":"string"}},"required":["bio"],"additionalProperties":false},
        {"type":"object","properties":{"links":{"type":"array"}},"required":["links"],"additionalProperties":false}]},
       "_meta":{"voucha/optionOutputPointers":{"bio":"#/anyOf/0","links":"#/anyOf/1"}}}
      """;

  [Fact]
  public void RealProducerResultFixturesValidateAgainstTheirOwnOutputSchemas()
  {
    using var manifest = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp.json")));
    var tools = manifest.RootElement.GetProperty("servers").EnumerateArray()
        .Single(server => server.GetProperty("name").GetString() == "voucha-user-mcp")
        .GetProperty("tools").EnumerateArray().Select(entry => entry.GetProperty("tool")).ToArray();
    using var catalog = JsonDocument.Parse(JsonSerializer.Serialize(new { tools }));
    Assert.Equal(tools.Length, new MemberMcpToolBridge(catalog.RootElement).Tools.Count);
    using var fixtures = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp-results.json")));
    foreach (var fixture in fixtures.RootElement.GetProperty("cases").EnumerateArray())
    {
      var tool = tools.Single(item => item.GetProperty("name").GetString() == fixture.GetProperty("tool").GetString());
      var contract = new MemberMcpOutputContract(tool);
      contract.Validate(Result(fixture.GetProperty("structuredContent")), fixture.GetProperty("arguments"));
    }
  }

  [Theory]
  [InlineData("native.mcp.read-my-profile.bio", "missing")]
  [InlineData("native.mcp.read-my-profile.bio", "null")]
  [InlineData("native.mcp.read-my-profile.links", "enum")]
  [InlineData("native.mcp.read-feed.posts", "date")]
  [InlineData("native.mcp.read-feed.posts", "boolean")]
  public void RealProducerResultsRejectMalformedFacts(string id, string mutation)
  {
    using var manifest = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp.json")));
    using var fixtures = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp-results.json")));
    var fixture = fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("id").GetString() == id);
    var tool = manifest.RootElement.GetProperty("servers").EnumerateArray()
        .Single(server => server.GetProperty("name").GetString() == "voucha-user-mcp")
        .GetProperty("tools").EnumerateArray().Select(row => row.GetProperty("tool"))
        .Single(item => item.GetProperty("name").GetString() == fixture.GetProperty("tool").GetString());
    var structured = JsonNode.Parse(fixture.GetProperty("structuredContent").GetRawText())!;
    switch (mutation)
    {
      case "missing": structured.AsObject().Remove("profile"); break;
      case "null": structured["profile"]!["markdown"] = null; break;
      case "enum": structured["results"]![0]!["link_type"] = "unknown"; break;
      case "date": structured["results"]![0]!["shared_at"] = "2025-02-29T00:00:00Z"; break;
      default: structured["page_info"]!["has_next_page"] = "false"; break;
    }
    using var invalid = JsonDocument.Parse(structured.ToJsonString());
    Assert.Throws<InvalidDataException>(() => new MemberMcpOutputContract(tool)
        .Validate(Result(invalid.RootElement), fixture.GetProperty("arguments")));
  }

  [Theory]
  [InlineData("meta")]
  [InlineData("mapping")]
  [InlineData("output")]
  public void RealCanonicalOptionToolRequiresMappingAndOutputSchema(string removed)
  {
    using var manifest = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp.json")));
    var original = manifest.RootElement.GetProperty("servers").EnumerateArray()
        .Single(server => server.GetProperty("name").GetString() == "voucha-user-mcp")
        .GetProperty("tools").EnumerateArray().Select(row => row.GetProperty("tool"))
        .Single(tool => tool.GetProperty("name").GetString() == "read_my_profile");
    var tool = JsonNode.Parse(original.GetRawText())!.AsObject();
    if (removed == "meta") tool.Remove("_meta");
    else if (removed == "output") tool.Remove("outputSchema");
    else tool["_meta"]!.AsObject().Remove("voucha/optionOutputPointers");
    using var catalog = JsonDocument.Parse(new JsonObject { ["tools"] = new JsonArray(tool) }.ToJsonString());
    Assert.Throws<InvalidDataException>(() => new MemberMcpToolBridge(catalog.RootElement));
  }

  [Fact]
  public void OrdinaryOneOfToolKeepsAdvertisedUnionWithoutAnOptionMap()
  {
    using var catalog = JsonDocument.Parse("""
        {"tools":[{"name":"measure","inputSchema":{"type":"object","oneOf":[
          {"properties":{"unit":{"const":"meters"}},"required":["unit"]},
          {"properties":{"unit":{"const":"feet"}},"required":["unit"]}]},
          "outputSchema":{"type":"object","properties":{"value":{"type":"number"}},"required":["value"],"additionalProperties":false}}]}
        """);
    Assert.Single(new MemberMcpToolBridge(catalog.RootElement).Tools);
  }

  [Fact]
  public void OutputDefinitionNamespaceCannotResolveAgainstInputDefinitions()
  {
    using var tool = JsonDocument.Parse("""
        {"inputSchema":{"type":"object","$defs":{"S1":{"type":"string"}}},
         "outputSchema":{"type":"object","properties":{"id":{"$ref":"#/$defs/S1"}},"required":["id"],"additionalProperties":false,"$defs":{"S1":{"type":"integer"}}}}
        """);
    var contract = new MemberMcpOutputContract(tool.RootElement);
    var arguments = JsonSerializer.SerializeToElement(new { });
    contract.Validate(Result(JsonSerializer.SerializeToElement(new { id = 12 })), arguments);
    foreach (var output in new[] { """{"id":"12"}""", """{"id":12,"extra":1}""", "{}", "null" })
    {
      using var value = JsonDocument.Parse(output);
      Assert.Throws<InvalidDataException>(() => contract.Validate(Result(value.RootElement), arguments));
    }
  }

  [Fact]
  public void ExplicitPointerRejectsAnotherOptionsOtherwiseValidOutputAndPreservesRefusals()
  {
    using var tool = JsonDocument.Parse(MappedTool);
    var contract = new MemberMcpOutputContract(tool.RootElement);
    var bioArguments = JsonSerializer.SerializeToElement(new { option = "bio" });
    contract.Validate(Result(JsonSerializer.SerializeToElement(new { bio = "Hello" })), bioArguments);
    var links = Result(JsonSerializer.SerializeToElement(new { links = Array.Empty<object>() }));
    contract.Validate(links, JsonSerializer.SerializeToElement(new { option = "links" }));
    Assert.Throws<InvalidDataException>(() => contract.Validate(links, bioArguments));
    contract.Validate(new McpToolResult(JsonSerializer.SerializeToElement(new[]
    {
      new { type = "text", text = "<external-content>refused</external-content>" },
    }), null, true), bioArguments);
  }

  [Theory]
  [InlineData("#/anyOf/9")]
  [InlineData("#/anyOf/00")]
  [InlineData("#/anyOf/-1")]
  [InlineData("https://example.com/schema")]
  public void MalformedOutputPointersFailCatalogValidation(string pointer)
  {
    var tool = JsonNode.Parse(MappedTool)!;
    tool["_meta"]!["voucha/optionOutputPointers"]!["bio"] = pointer;
    using var catalog = JsonDocument.Parse("{\"tools\":[" + tool.ToJsonString() + "]}");
    Assert.Throws<InvalidDataException>(() => new MemberMcpToolBridge(catalog.RootElement));
  }

  [Fact]
  public void MissingOrExtraOptionsFailCatalogValidation()
  {
    foreach (var remove in new[] { true, false })
    {
      var tool = JsonNode.Parse(MappedTool)!;
      var mapping = tool["_meta"]!["voucha/optionOutputPointers"]!.AsObject();
      if (remove) mapping.Remove("links");
      else mapping["unknown"] = "#";
      using var catalog = JsonDocument.Parse("{\"tools\":[" + tool.ToJsonString() + "]}");
      Assert.Throws<InvalidDataException>(() => new MemberMcpToolBridge(catalog.RootElement));
    }
  }

  [Fact]
  public void DuplicateOptionLiteralsCannotShareOneOutputMapping()
  {
    var tool = JsonNode.Parse(MappedTool)!;
    tool["inputSchema"]!["oneOf"]![1]!["properties"]!["option"]!["enum"]![0] = "bio";
    tool["_meta"]!["voucha/optionOutputPointers"]!.AsObject().Remove("links");
    using var catalog = JsonDocument.Parse(new JsonObject { ["tools"] = new JsonArray(tool) }.ToJsonString());
    Assert.Throws<InvalidDataException>(() => new MemberMcpToolBridge(catalog.RootElement));
  }

  [Theory]
  [InlineData("required")]
  [InlineData("properties")]
  [InlineData("additionalProperties")]
  public void SelectedOutputMustAlsoSatisfyAdvertisedRootConstraints(string constraint)
  {
    var tool = JsonNode.Parse(MappedTool)!;
    var output = tool["outputSchema"]!;
    var option = "links";
    var result = "{\"links\":[]}";
    if (constraint == "required") output["required"] = new JsonArray("bio");
    else
    {
      output["properties"] = JsonNode.Parse("""{"bio":{"type":"string","minLength":5}}""");
      if (constraint == "additionalProperties") output["additionalProperties"] = false;
      else { option = "bio"; result = "{\"bio\":\"x\"}"; }
    }
    using var document = JsonDocument.Parse(tool.ToJsonString());
    using var invalid = JsonDocument.Parse(result);
    Assert.Throws<InvalidDataException>(() => new MemberMcpOutputContract(document.RootElement)
        .Validate(Result(invalid.RootElement), JsonSerializer.SerializeToElement(new { option })));
    new MemberMcpOutputContract(document.RootElement).Validate(
        Result(JsonSerializer.SerializeToElement(new { bio = "Hello" })),
        JsonSerializer.SerializeToElement(new { option = "bio" }));
  }

  [Theory]
  [InlineData("number", "1e100", true)]
  [InlineData("number", "-1e100", true)]
  [InlineData("number", "1e-100", true)]
  [InlineData("integer", "1e100", true)]
  [InlineData("integer", "1e-100", false)]
  public void UnconstrainedOutputNumbersDoNotRequireDecimalRange(string type, string raw, bool expected)
  {
    using var schema = JsonDocument.Parse(JsonSerializer.Serialize(new { type }));
    using var value = JsonDocument.Parse(raw);
    Assert.Equal(expected, MemberMcpInputSchema.AcceptsOutput(value.RootElement, schema.RootElement, schema.RootElement));
    Assert.False(MemberMcpInputSchema.Accepts(value.RootElement, schema.RootElement));
  }

  [Theory]
  [InlineData("1e100", "minimum", "0", true)]
  [InlineData("-1e100", "maximum", "0", true)]
  [InlineData("1e100", "maximum", "0", false)]
  [InlineData("-1e100", "minimum", "0", false)]
  [InlineData("1e-100", "minimum", "0", true)]
  [InlineData("1e-100", "maximum", "0", false)]
  [InlineData("0", "minimum", "-1", true)]
  [InlineData("0", "maximum", "-1", false)]
  [InlineData("-12.34", "minimum", "-12.35", true)]
  [InlineData("12.34", "maximum", "12.35", true)]
  [InlineData("12.34", "minimum", "12.35", false)]
  public void OutputNumericBoundsCompareExactlyOutsideDecimalRange(string raw, string keyword, string bound, bool expected)
  {
    using var schema = JsonDocument.Parse($"{{\"type\":\"number\",\"{keyword}\":{bound}}}");
    using var value = JsonDocument.Parse(raw);
    Assert.Equal(expected, MemberMcpInputSchema.AcceptsOutput(value.RootElement, schema.RootElement, schema.RootElement));
  }

  [Theory]
  [InlineData("2026-10-09T00:00:00.000Z", true)]
  [InlineData("2024-02-29T23:59:59+02:00", true)]
  [InlineData("2026-10-09\uFEFF00:00:00Z", true)]
  [InlineData("2026-10-09\u008500:00:00Z", false)]
  [InlineData("0000-02-29T12:00:00Z", true)]
  [InlineData("2017-01-01T00:59:60+01:00", true)]
  [InlineData("2016-12-31T23:59:60Z", true)]
  [InlineData("2026-10-09T23:59:60Z", true)]
  [InlineData("2017-01-01T00:29:60+00:30", true)]
  [InlineData("2016-12-31T23:29:60-00:30", true)]
  [InlineData("2016-12-31T23:59:60.5Z", true)]
  [InlineData("2026-10-09 00:00:00+0000", true)]
  [InlineData("2016-12-31T12:00:60Z", false)]
  [InlineData("2016-12-31T23:59:61Z", false)]
  [InlineData("2025-02-29T00:00:00Z", false)]
  [InlineData("2026-10-09", false)]
  [InlineData("2026-10-09T00:00:00", false)]
  [InlineData("2026-10-09T24:00:00Z", false)]
  [InlineData("2026-10-09T00:00:00Z\n", false)]
  public void OutputDateTimeIsCalendarValidAndInputSubsetIsUnchanged(string timestamp, bool expected)
  {
    using var schema = JsonDocument.Parse("""{"type":"string","format":"date-time"}""");
    Assert.Throws<InvalidDataException>(() => MemberMcpInputSchema.Inspect(schema.RootElement));
    MemberMcpInputSchema.Inspect(schema.RootElement, output: true);
    Assert.Equal(expected, MemberMcpInputSchema.AcceptsOutput(JsonSerializer.SerializeToElement(timestamp),
        schema.RootElement, schema.RootElement));
  }

  internal static McpToolResult Result(JsonElement structured) =>
      new(JsonSerializer.SerializeToElement(Array.Empty<object>()), structured, false);
}

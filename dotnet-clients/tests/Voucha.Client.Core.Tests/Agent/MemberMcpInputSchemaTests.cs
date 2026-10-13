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

  private static bool Accepts(string rawValue, JsonDocument schema)
  {
    using var value = JsonDocument.Parse(rawValue);
    return MemberMcpInputSchema.Accepts(value.RootElement, schema.RootElement);
  }
}

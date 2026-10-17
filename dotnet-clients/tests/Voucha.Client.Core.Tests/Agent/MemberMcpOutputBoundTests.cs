using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpOutputBoundTests
{
  [Fact]
  public void LargeAdvertisedBoundsAreAdmittedAndEnforcedWithoutChangingInputSubset()
  {
    var tool = JsonNode.Parse(MemberMcpOutputContractTests.MappedTool)!;
    tool["outputSchema"] = JsonNode.Parse("""{"type":"number","minimum":-1e300,"maximum":1e300}""");
    tool["_meta"]!["voucha/optionOutputPointers"] = JsonNode.Parse("""{"bio":"#","links":"#"}""");
    using var catalog = JsonDocument.Parse(tool.ToJsonString());
    var contract = new MemberMcpOutputContract(catalog.RootElement);
    using var arguments = JsonDocument.Parse("""{"option":"bio","arguments":{}}""");
    using var content = JsonDocument.Parse("[]");
    using var valid = JsonDocument.Parse("1e300");
    using var invalid = JsonDocument.Parse("2e300");
    contract.Validate(new McpToolResult(content.RootElement, valid.RootElement, false), arguments.RootElement);
    Assert.Throws<InvalidDataException>(() =>
        contract.Validate(new McpToolResult(content.RootElement, invalid.RootElement, false), arguments.RootElement));
    Assert.Throws<InvalidDataException>(() =>
        MemberMcpInputSchema.Inspect(catalog.RootElement.GetProperty("outputSchema")));
  }
  [Fact]
  public void SelectedAnyOfUsesOneTraversalWhileEnforcingRootAndSelectedConstraints()
  {
    var tool = JsonNode.Parse(MemberMcpOutputContractTests.MappedTool)!;
    tool["outputSchema"] = JsonNode.Parse("""
        {"type":"array","maxItems":150000,"anyOf":[{"items":{"type":"null"}},{"items":{"type":"string"}}]}
        """);
    using var catalog = JsonDocument.Parse(tool.ToJsonString());
    var contract = new MemberMcpOutputContract(catalog.RootElement);
    using var arguments = JsonDocument.Parse("""{"option":"bio","arguments":{}}""");
    using var content = JsonDocument.Parse("[]");
    using var valid = JsonDocument.Parse("[" + string.Join(',', Enumerable.Repeat("null", 140_000)) + "]");
    contract.Validate(new McpToolResult(content.RootElement, valid.RootElement, false), arguments.RootElement);
    using var wrongBranch = JsonDocument.Parse("""["other branch"]""");
    Assert.Throws<InvalidDataException>(() => contract.Validate(
        new McpToolResult(content.RootElement, wrongBranch.RootElement, false), arguments.RootElement));
    tool["outputSchema"]!["maxItems"] = 0;
    using var constrained = JsonDocument.Parse(tool.ToJsonString());
    Assert.Throws<InvalidDataException>(() => new MemberMcpOutputContract(constrained.RootElement).Validate(
        new McpToolResult(content.RootElement, valid.RootElement, false), arguments.RootElement));
  }

  [Fact]
  public void OutputUniquenessCannotSpendUnboundedRecursiveComparisonWork()
  {
    using var schema = JsonDocument.Parse("""{"type":"array","uniqueItems":true,"maxItems":256}""");
    var values = Enumerable.Range(0, 256).Select(index =>
        Enumerable.Repeat(0, 799).Append(index).ToArray()).ToArray();
    using var result = JsonDocument.Parse(JsonSerializer.Serialize(values));
    Assert.Throws<InvalidDataException>(() =>
        MemberMcpInputSchema.AcceptsOutput(result.RootElement, schema.RootElement, schema.RootElement));
    using var ordinary = JsonDocument.Parse("[[0,1],[0,2]]");
    Assert.True(MemberMcpInputSchema.AcceptsOutput(ordinary.RootElement, schema.RootElement, schema.RootElement));
  }

  [Fact]
  public void NestedDefinitionVisitCannotSatisfySelectedRootBranch()
  {
    using var schema = JsonDocument.Parse("""
        {"anyOf":[{"$ref":"#/$defs/a"},{"type":"object","properties":{"nested":{"$ref":"#/$defs/a"}},
        "required":["nested"]}],"$defs":{"a":{"type":"object","properties":{"kind":{"const":"a"}},
        "required":["kind"]}}}
        """);
    using var result = JsonDocument.Parse("""{"nested":{"kind":"a"}}""");
    Assert.False(MemberMcpInputSchema.AcceptsOutput(result.RootElement, schema.RootElement, schema.RootElement, 0));
  }

  [Fact]
  public void OutputEqualityChargesLongStringsAndWideObjectConstruction()
  {
    using var schema = JsonDocument.Parse("""{"type":"array","uniqueItems":true,"maxItems":256}""");
    var strings = Enumerable.Range(0, 256).Select(index => new string('x', 4000) + index).ToArray();
    using var text = JsonDocument.Parse(JsonSerializer.Serialize(strings));
    Assert.Throws<InvalidDataException>(() =>
        MemberMcpInputSchema.AcceptsOutput(text.RootElement, schema.RootElement, schema.RootElement));
    var objects = Enumerable.Range(0, 256).Select(index =>
    {
      var fields = new Dictionary<string, int>(StringComparer.Ordinal) { ["index"] = index };
      foreach (var key in Enumerable.Range(0, 100)) fields["key" + key] = 0;
      return fields;
    }).ToArray();
    using var wide = JsonDocument.Parse(JsonSerializer.Serialize(objects));
    Assert.Throws<InvalidDataException>(() =>
        MemberMcpInputSchema.AcceptsOutput(wide.RootElement, schema.RootElement, schema.RootElement));
    using var distinct = JsonDocument.Parse("""["é","e\u0301"]""");
    Assert.True(MemberMcpInputSchema.AcceptsOutput(distinct.RootElement, schema.RootElement, schema.RootElement));
  }

}

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
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class CopyrightMcpResultFixtureTests
{
  private static JsonNode StructuredContent(string id)
  {
    var catalog = JsonNode.Parse(File.ReadAllText(ContractPaths.ApiFixture("mcp-results.json")))!;
    Assert.Equal(1, catalog["version"]!.GetValue<int>());
    var selected = catalog["cases"]!.AsArray()
        .Single(value => value!["id"]!.GetValue<string>() == id)!;
    return selected["structuredContent"]!.DeepClone();
  }

  [Theory]
  [InlineData("native.mcp.search-copyright-notices-for-association.results")]
  [InlineData("native.mcp.list-repeat-infringer-accounts.accounts")]
  public void ResultsValidateAgainstTheLegalServerOutputSchemas(string id)
  {
    using var manifest = JsonDocument.Parse(File.ReadAllText(ContractPaths.ApiFixture("mcp.json")));
    using var catalog = JsonDocument.Parse(File.ReadAllText(ContractPaths.ApiFixture("mcp-results.json")));
    var fixture = catalog.RootElement.GetProperty("cases").EnumerateArray()
        .Single(row => row.GetProperty("id").GetString() == id);
    var tool = manifest.RootElement.GetProperty("servers").EnumerateArray()
        .Single(server => server.GetProperty("name").GetString() == "voucha-legal-mcp")
        .GetProperty("tools").EnumerateArray().Select(row => row.GetProperty("tool"))
        .Single(item => item.GetProperty("name").GetString() == fixture.GetProperty("tool").GetString());
    new MemberMcpOutputContract(tool).Validate(
        MemberMcpOutputContractTests.Result(fixture.GetProperty("structuredContent")),
        fixture.GetProperty("arguments"));
  }

  [Fact]
  public void AssociationSearchResultDecodesWithOptionalWorkDescription()
  {
    var body = StructuredContent("native.mcp.search-copyright-notices-for-association.results");
    var decoded = JsonSerializer.Deserialize<CopyrightAssociationTargetsResponse>(body, VouchaApiJson.Options)!;
    var notice = Assert.Single(decoded.CopyrightNotices);
    Assert.Equal("0198ffff-0000-7000-8000-000000000031", notice.Id);
    Assert.Equal("Example Claimant", notice.ClaimantDisplayName);
    Assert.Equal("Example work", notice.WorkDescription);
    Assert.False(decoded.PageInfo.HasNextPage);
    Assert.Null(decoded.PageInfo.EndCursor);

    var notices = body["copyright_notices"]!.AsArray();
    notices[0]!["work_description"] = null;
    decoded = JsonSerializer.Deserialize<CopyrightAssociationTargetsResponse>(body, VouchaApiJson.Options)!;
    Assert.Null(Assert.Single(decoded.CopyrightNotices).WorkDescription);
    var encoded = JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!;
    Assert.True(encoded["copyright_notices"]![0]!.AsObject().ContainsKey("work_description"));

    notices[0]!.AsObject().Remove("work_description");
    Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<CopyrightAssociationTargetsResponse>(body, VouchaApiJson.Options));
  }

  [Fact]
  public void RepeatInfringerAccountsResultDecodesUsername()
  {
    var body = StructuredContent("native.mcp.list-repeat-infringer-accounts.accounts");
    var decoded = JsonSerializer.Deserialize<CopyrightRepeatInfringerAccountsResponse>(body, VouchaApiJson.Options)!;
    var account = Assert.Single(decoded.Accounts);
    Assert.Equal("example-account", account.AccountUsername);
    Assert.Equal("0198ffff-0000-7000-8000-000000000032", account.AccountUserId);
    Assert.Equal("0198ffff-0000-7000-8000-000000000033", account.IncidentId);
    Assert.True(account.IsOperative);
    Assert.Equal("0198ffff-0000-7000-8000-000000000034", account.OpenReviewId);
    Assert.False(account.TerminationInEffect);
  }
}

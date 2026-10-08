using System.Text.Json;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class FinancialMcpManifestContractTests
{
  [Fact]
  public void PublishedToolsSeparateFinancialReadsFromTheGeneralProfile()
  {
    using var manifest = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp.json")));
    var tools = manifest.RootElement.GetProperty("servers")[0].GetProperty("tools");
    var general = Tool(tools, "get_my_profile");
    var financial = Tool(tools, "get_my_financial_profile");

    Assert.Equal(["profile:read"], RequiredScopes(general));
    Assert.Equal(["financial-profile:read"], RequiredScopes(financial));
    Assert.DoesNotContain("financial_profile", PropertyNames(general.GetProperty("outputSchema")));
    Assert.DoesNotContain("credit_score_range", PropertyNames(general.GetProperty("outputSchema")));

    var result = financial.GetProperty("outputSchema").GetProperty("properties").GetProperty("result");
    var profile = result.GetProperty("properties").GetProperty("financial_profile");
    Assert.Contains("financial_profile", result.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
    Assert.Contains(profile.GetProperty("anyOf").EnumerateArray(), branch =>
        branch.GetProperty("type").GetString() == "null");
    Assert.Contains(profile.GetProperty("anyOf").EnumerateArray(), branch =>
        branch.GetProperty("type").GetString() == "object" &&
        branch.GetProperty("properties").TryGetProperty("credit_score_range", out _));
  }

  private static JsonElement Tool(JsonElement tools, string name) =>
      tools.EnumerateArray().Single(entry => entry.GetProperty("tool").GetProperty("name").GetString() == name)
          .GetProperty("tool");

  private static string[] RequiredScopes(JsonElement tool) =>
      tool.GetProperty("_meta").GetProperty("voucha/requiredScopes").EnumerateArray()
          .Select(value => value.GetString() ?? throw new InvalidOperationException("Missing required scope."))
          .ToArray();

  private static IEnumerable<string> PropertyNames(JsonElement value)
  {
    if (value.ValueKind == JsonValueKind.Object)
    {
      foreach (var property in value.EnumerateObject())
      {
        yield return property.Name;
        foreach (var nested in PropertyNames(property.Value)) yield return nested;
      }
    }
    else if (value.ValueKind == JsonValueKind.Array)
    {
      foreach (var item in value.EnumerateArray())
        foreach (var nested in PropertyNames(item)) yield return nested;
    }
  }
}

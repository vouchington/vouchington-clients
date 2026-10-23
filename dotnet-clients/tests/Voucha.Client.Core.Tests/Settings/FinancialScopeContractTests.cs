using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class FinancialScopeContractTests
{
  [Fact]
  public void PublishedCatalogKeepsFinancialGrantsExactAndDescriptionsLocalized()
  {
    var catalog = JsonSerializer.Deserialize<ScopeCatalogResponse>(
        ApiFixtureLoader.LoadResponse("shared.scopes.catalog"), VouchaApiJson.Options)!;
    var expected = new (string Scope, string DescriptionKey, string? Requires)[]
    {
      ("financial-profile:read", "financial_profile_read", null),
      ("financial-profile:write", "financial_profile_write", "financial-profile:read"),
      ("spending:read", "spending_read", null),
      ("spending:write", "spending_write", "spending:read"),
    };

    foreach (var (scope, descriptionKey, requires) in expected)
    {
      var entry = Assert.Single(catalog.Scopes, item => item.Scope == scope);
      Assert.Equal(descriptionKey, entry.DescriptionKey);
      Assert.Equal(requires, entry.Requires);
      Assert.Contains("api-key", entry.Surfaces);
      Assert.Contains("oauth", entry.Surfaces);
      Assert.NotEmpty(new SettingsScopeRow(entry, false, UiLocalization.English).Description);
    }

    var selection = new ApiKeyScopeSelection(catalog.Scopes.Where(entry =>
        entry.Audience == "user" && entry.Surfaces.Contains("api-key")).ToArray());
    selection.SetSelected("mcp.user:read", true);
    Assert.Equal(["mcp.user:read"], selection.SelectedScopes);
    selection.SetSelected("financial-profile:write", true);
    Assert.Equal(["financial-profile:read", "financial-profile:write", "mcp.user:read"],
        selection.SelectedScopes.Order(StringComparer.Ordinal));
    selection.SetSelected("financial-profile:read", false);
    Assert.Equal(["mcp.user:read"], selection.SelectedScopes);
    selection.SetSelected("spending:write", true);
    Assert.Equal(["mcp.user:read", "spending:read", "spending:write"],
        selection.SelectedScopes.Order(StringComparer.Ordinal));
  }
}

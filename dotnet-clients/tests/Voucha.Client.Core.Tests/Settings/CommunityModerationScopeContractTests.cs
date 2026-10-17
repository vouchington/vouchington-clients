using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class CommunityModerationScopeContractTests
{
  [Fact]
  public void PublishedModerationScopesKeepExactDescriptionsAndReadPrerequisite()
  {
    var catalog = JsonSerializer.Deserialize<ScopeCatalogResponse>(
        ApiFixtureLoader.LoadResponse("shared.scopes.catalog"), VouchaApiJson.Options)!;
    var read = Assert.Single(catalog.Scopes, entry => entry.Scope == "community-moderation:read");
    var write = Assert.Single(catalog.Scopes, entry => entry.Scope == "community-moderation:write");
    Assert.Equal("community_moderation_read", read.DescriptionKey);
    Assert.Equal("community_moderation_write", write.DescriptionKey);
    Assert.Null(read.Requires);
    Assert.Equal(read.Scope, write.Requires);
    Assert.Contains("api-key", write.Surfaces);
    Assert.Contains("oauth", write.Surfaces);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeCredentialsCommunityModerationRead),
        new SettingsScopeRow(read, false, UiLocalization.English).Description);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeCredentialsCommunityModerationWrite),
        new SettingsScopeRow(write, false, UiLocalization.English).Description);

    var selection = new ApiKeyScopeSelection(catalog.Scopes.Where(entry =>
        entry.Audience == "user" && entry.Surfaces.Contains("api-key")).ToArray());
    selection.SetSelected("mcp.user:read", true);
    Assert.Equal(["mcp.user:read"], selection.SelectedScopes);
    selection.SetSelected(write.Scope, true);
    Assert.Equal([read.Scope, write.Scope, "mcp.user:read"], selection.SelectedScopes.Order(StringComparer.Ordinal));
    selection.SetSelected(read.Scope, false);
    Assert.Equal(["mcp.user:read"], selection.SelectedScopes);
  }
}

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task MemberAudienceChoicesKeepCreationAndSelectionIsolated()
  {
    var audiences = new[] { "user", "community_moderation", "brand", "legal", "admin", "api", "future" };
    var service = new FakeSettingsService
    {
      Catalog = new ScopeCatalogResponse(audiences.Select(audience => new ScopeCatalogEntry(
          $"{audience}:read", audience, "fixture", "read", ["api-key"], null, null)).ToArray()),
    };
    using var model = new SettingsViewModel(service) { ApiKeyType = "mcp", ApiKeyLabel = "Fixture" };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["user:read", "community_moderation:read", "brand:read"], model.ApiKeyScopes.Select(row => row.Scope));
    model.SetApiKeyScopeSelected("community_moderation:read", true);
    Assert.True(model.CanCreateApiKey);
    Assert.Equal("community_moderation", Assert.Single(model.ApiKeyScopes, row => row.IsSelected).Audience);
    model.SetApiKeyScopeSelected("user:read", true);
    Assert.Equal(["community_moderation:read"], model.SelectedApiKeyScopes);
    Assert.NotNull(model.CredentialNotice);
    model.ApiKeyType = "rss";
    Assert.Empty(model.SelectedApiKeyScopes);
    Assert.Equal("api:read", Assert.Single(model.ApiKeyScopes).Scope);
  }
}

using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task UnrelatedSettingsFailureDoesNotBlockCredentials()
  {
    var service = new FakeSettingsService
    {
      MembershipPlansFailure = new HttpRequestException("plans offline"),
      Grants = Task.FromResult(new OAuthGrantListResponse([Grant("connected")], new PageInfo(null, false, null))),
    };
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader" };

    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains("plans offline", model.ErrorMessage, StringComparison.Ordinal);
    Assert.Equal("rss:read", Assert.Single(model.ApiKeyScopes).Scope);
    model.SetApiKeyScopeSelected("rss:read", true);
    Assert.True(model.CanCreateApiKey);
    Assert.Equal("connected", Assert.Single(model.OAuthGrants).Id);
  }

  [Fact]
  public async Task FailedUserReloadCannotRetainPreviousAdministratorScopes()
  {
    var service = new FakeSettingsService { Roles = ["administrator"] };
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.SelectedApiKeyAudienceOption = model.ApiKeyAudienceOptions.Single(option => option.ProtocolValue == "admin");
    Assert.Equal("mcp.admin:read", Assert.Single(model.ApiKeyScopes).Scope);
    service.UserFailure = new HttpRequestException("user offline");

    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(model.CanSelectAdminApiKeyScopes);
    Assert.Empty(model.ApiKeyScopes);
    Assert.False(model.CanCreateApiKey);
  }

  [Fact]
  public async Task ScopeChoicesAreCatalogueDrivenEmptyAndAudienceGated()
  {
    var service = new FakeSettingsService();
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader" };
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("rss:read", Assert.Single(model.ApiKeyScopes).Scope);
    Assert.Empty(model.SelectedApiKeyScopes);
    Assert.False(model.CanCreateApiKey);
    model.SetApiKeyScopeSelected("rss:read", true);
    Assert.True(model.CanCreateApiKey);
    var currentAudience = model.SelectedApiKeyAudienceOption;
    model.SelectedApiKeyAudienceOption = currentAudience;
    Assert.Equal(["rss:read"], model.SelectedApiKeyScopes);
    model.ApiKeyType = "mcp";
    Assert.Empty(model.SelectedApiKeyScopes);
    Assert.Equal(["mcp.user:read", "mcp.user:write"], model.ApiKeyScopes.Select(scope => scope.Scope));
    Assert.False(model.CanSelectAdminApiKeyScopes);
    Assert.Equal("user", Assert.Single(model.ApiKeyAudienceOptions).ProtocolValue);
  }

  [Fact]
  public async Task AdminScopesRequireTheAdministratorRoleAndExplicitAudienceSelection()
  {
    var service = new FakeSettingsService { Roles = ["administrator"] };
    using var model = new SettingsViewModel(service) { ApiKeyType = "mcp" };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.CanSelectAdminApiKeyScopes);
    Assert.DoesNotContain(model.ApiKeyScopes, scope => scope.ProtocolValue.Audience == "admin");

    model.SelectedApiKeyAudienceOption = model.ApiKeyAudienceOptions.Single(option => option.ProtocolValue == "admin");

    Assert.Equal("mcp.admin:read", Assert.Single(model.ApiKeyScopes).Scope);
    Assert.Empty(model.SelectedApiKeyScopes);
    service.Roles = [];
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(model.CanSelectAdminApiKeyScopes);
    Assert.All(model.ApiKeyScopes, scope => Assert.Equal("user", scope.ProtocolValue.Audience));
  }

  [Fact]
  public async Task FailedCatalogueLoadClearsPreviouslySelectablePermissions()
  {
    var service = new FakeSettingsService();
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader" };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.SetApiKeyScopeSelected("rss:read", true);
    service.CatalogFailure = new HttpRequestException("offline");

    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(model.ApiKeyScopes);
    Assert.False(model.CanCreateApiKey);
    Assert.NotNull(model.CredentialNotice);
  }

  [Fact]
  public async Task GrantsAppendDeduplicateAndFailedRevocationRetainsTheGrant()
  {
    var first = Grant("first");
    var second = Grant("second");
    var service = new FakeSettingsService
    {
      FetchGrantsPage = after => Task.FromResult(after is null
          ? new OAuthGrantListResponse([first], new PageInfo("opaque/+cursor", true, null))
          : new OAuthGrantListResponse([first, second], new PageInfo(null, false, null))),
      RevokeGrant = () => Task.FromException(new HttpRequestException("offline")),
    };
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreOAuthGrantsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("opaque/+cursor", service.LastGrantCursor);
    Assert.Equal(["first", "second"], model.OAuthGrants.Select(grant => grant.Id));
    Assert.Equal(
        UiLocalization.English.Localize(UiMessageKey.NativeCredentialsUnverified),
        model.LocalizedOAuthGrants[0].Verification);
    Assert.False(model.HasMoreOAuthGrants);
    await model.RevokeOAuthGrantAsync(first, TestContext.Current.CancellationToken);
    Assert.Equal(2, model.OAuthGrants.Count);
    Assert.Equal("first", service.LastRevokedGrant);
    Assert.NotNull(model.OAuthGrantNotice);
    service.RevokeGrant = () => Task.CompletedTask;
    await model.RevokeOAuthGrantAsync(first, TestContext.Current.CancellationToken);
    Assert.Equal("second", Assert.Single(model.OAuthGrants).Id);
  }

  [Fact]
  public async Task InitialGrantTimeoutShowsRetryAndDoesNotClaimAnEmptyList()
  {
    var service = new FakeSettingsService { Grants = Task.FromException<OAuthGrantListResponse>(new OperationCanceledException("timeout")) };
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(model.HasOAuthGrantPaginationError);
    Assert.True(model.HasMoreOAuthGrants);
    Assert.False(model.IsLoadingMoreOAuthGrants);
    Assert.False(model.HasNoOAuthGrants);
    service.Grants = Task.FromResult(new OAuthGrantListResponse([Grant("retry")], new PageInfo(null, false, null)));
    await model.LoadMoreOAuthGrantsAsync(TestContext.Current.CancellationToken);
    Assert.Equal("retry", Assert.Single(model.OAuthGrants).Id);
    Assert.False(model.HasOAuthGrantPaginationError);
  }

  [Fact]
  public async Task FailedGrantRefreshRetryReplacesThePreviouslyVisiblePage()
  {
    var service = new FakeSettingsService
    {
      Grants = Task.FromResult(new OAuthGrantListResponse([Grant("stale")], new PageInfo(null, false, null))),
    };
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.Grants = Task.FromException<OAuthGrantListResponse>(new HttpRequestException("offline"));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("stale", Assert.Single(model.OAuthGrants).Id);
    Assert.True(model.HasOAuthGrantPaginationError);
    service.Grants = Task.FromResult(SettingsCredentialTestFixtures.EmptyGrants);

    await model.LoadMoreOAuthGrantsAsync(TestContext.Current.CancellationToken);

    Assert.Empty(model.OAuthGrants);
    Assert.True(model.HasNoOAuthGrants);
    Assert.False(model.HasOAuthGrantPaginationError);
  }

  [Fact]
  public async Task MissingGrantReconcilesButAuthorizationFailureRetainsTheRow()
  {
    var grant = Grant("first");
    var service = new FakeSettingsService
    {
      Grants = Task.FromResult(new OAuthGrantListResponse([grant], new PageInfo(null, false, null))),
      RevokeGrant = () => Task.FromException(new VouchaApiException(HttpStatusCode.Forbidden, "{}")),
    };
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.RevokeOAuthGrantAsync(grant, TestContext.Current.CancellationToken);
    Assert.Equal("first", Assert.Single(model.OAuthGrants).Id);

    service.RevokeGrant = () => Task.FromException(new VouchaApiException(HttpStatusCode.NotFound, "{}"));
    await model.RevokeOAuthGrantAsync(grant, TestContext.Current.CancellationToken);
    Assert.Empty(model.OAuthGrants);
    Assert.True(model.HasNoOAuthGrants);
  }

  [Fact]
  public async Task RevokingLastVisibleGrantDoesNotClaimEmptyWhileAnotherPageExists()
  {
    var grant = Grant("first");
    var service = new FakeSettingsService
    {
      Grants = Task.FromResult(new OAuthGrantListResponse([grant], new PageInfo("next", true, null))),
    };
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    await model.RevokeOAuthGrantAsync(grant, TestContext.Current.CancellationToken);

    Assert.Empty(model.OAuthGrants);
    Assert.True(model.HasMoreOAuthGrants);
    Assert.False(model.HasNoOAuthGrants);
  }

  [Fact]
  public async Task UnknownCatalogueDescriptionsFailTruthfullyWithoutRawPresentationCopy()
  {
    var service = new FakeSettingsService
    {
      Catalog = new ScopeCatalogResponse([new("rss:read", "api", "rss", "read", ["api-key"], "unknown_description", null)]),
    };
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader" };
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(model.ApiKeyScopes);
    Assert.False(model.CanCreateApiKey);
    Assert.NotNull(model.CredentialNotice);
    Assert.DoesNotContain("unknown_description", model.CredentialNotice, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("financial_profile_read")]
  [InlineData("financial_profile_write")]
  [InlineData("spending_read")]
  [InlineData("spending_write")]
  public async Task SupportedOAuthDescriptionsDoNotDisableApiKeyCatalog(string descriptionKey)
  {
    var oauthScope = new ScopeCatalogEntry("oauth-only:read", "user", "oauth-only", "read", ["oauth"], descriptionKey, null);
    var service = new FakeSettingsService
    {
      Catalog = new ScopeCatalogResponse([
          new("rss:read", "api", "rss", "read", ["api-key"], null, null), oauthScope,
      ]),
    };
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader" };
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("rss:read", Assert.Single(model.ApiKeyScopes).Scope);
    Assert.Equal(string.Empty, new SettingsScopeRow(oauthScope, false, UiLocalization.English).Description);
    model.SetApiKeyScopeSelected("rss:read", true);
    Assert.True(model.CanCreateApiKey);
    Assert.Null(model.CredentialNotice);
  }

  [Fact]
  public async Task SuccessfulRevocationCannotBeUndoneByAnOlderPageResponse()
  {
    var grant = Grant("first");
    var page = new TaskCompletionSource<OAuthGrantListResponse>();
    var service = new FakeSettingsService
    {
      FetchGrantsPage = after => after is null
          ? Task.FromResult(new OAuthGrantListResponse([grant], new PageInfo("next", true, null)))
          : page.Task,
    };
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var pendingPage = model.LoadMoreOAuthGrantsAsync(TestContext.Current.CancellationToken);
    await model.RevokeOAuthGrantAsync(grant, TestContext.Current.CancellationToken);
    page.SetResult(new OAuthGrantListResponse([grant], new PageInfo(null, false, null)));
    await pendingPage;

    Assert.Empty(model.OAuthGrants);
    Assert.True(model.HasNoOAuthGrants);
  }

  [Fact]
  public async Task EmptySelectionDoesNotCreateAKey()
  {
    using var model = new SettingsViewModel(new FakeSettingsService()) { ApiKeyLabel = "Reader" };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.CreateApiKeyAsync(TestContext.Current.CancellationToken);

    Assert.Null(model.ApiKeySecret);
    Assert.NotNull(model.CredentialNotice);
  }

  private static OAuthGrant Grant(string id) => new(id, new("client", "public-client", "Client", false),
      "https://voucha.ai/api/v1/mcp", ["mcp.user:read"], DateTimeOffset.UnixEpoch, null);
}

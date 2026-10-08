using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task HeldCatalogDoesNotDelaySettingsContentOrCompletedGrants()
  {
    var catalog = new TaskCompletionSource<ScopeCatalogResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeSettingsService
    {
      CatalogTask = catalog.Task,
      Grants = Task.FromResult(new OAuthGrantListResponse([Grant("connected")], new PageInfo(null, false, null))),
    };
    using var model = new SettingsViewModel(service);
    var load = model.LoadAsync(TestContext.Current.CancellationToken);
    try
    {
      Assert.False(load.IsCompleted);
      Assert.False(model.IsLoading);
      Assert.NotEmpty(model.ApiKeys);
      Assert.NotEmpty(model.Sessions);
      Assert.Equal("connected", Assert.Single(model.OAuthGrants).Id);
      Assert.Empty(model.ApiKeyScopes);
    }
    finally
    {
      catalog.TrySetResult(SettingsCredentialTestFixtures.Catalog);
      await load;
    }
    Assert.Equal("rss:read", Assert.Single(model.ApiKeyScopes).Scope);
  }

  [Fact]
  public async Task HeldGrantsDoNotDelaySettingsContentOrCompletedCatalog()
  {
    var grants = new TaskCompletionSource<OAuthGrantListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeSettingsService { Grants = grants.Task };
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader" };
    var load = model.LoadAsync(TestContext.Current.CancellationToken);
    try
    {
      Assert.False(load.IsCompleted);
      Assert.False(model.IsLoading);
      Assert.NotEmpty(model.ApiKeys);
      Assert.NotEmpty(model.Sessions);
      Assert.Equal("rss:read", Assert.Single(model.ApiKeyScopes).Scope);
      model.SetApiKeyScopeSelected("rss:read", true);
      Assert.True(model.CanCreateApiKey);
    }
    finally
    {
      grants.TrySetResult(SettingsCredentialTestFixtures.EmptyGrants);
      await load;
    }
  }

  [Fact]
  public async Task HeldUserDoesNotDelayIndependentSettingsContentOrExposeAuthorization()
  {
    var user = new TaskCompletionSource<UserResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var userStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var profileStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeSettingsService
    {
      UserTask = user.Task,
      UserFetchStarted = userStarted,
      ProfileFetchStarted = profileStarted,
      Roles = ["administrator"],
    };
    using var model = new SettingsViewModel(service) { ApiKeyType = "mcp", ApiKeyLabel = "Reader" };
    var load = model.LoadAsync(TestContext.Current.CancellationToken);
    try
    {
      await userStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.True(profileStarted.Task.IsCompleted);

      Assert.False(load.IsCompleted);
      Assert.True(model.IsLoading);
      Assert.Equal("Hello, Voucha!", model.ProfileMarkdown);
      Assert.NotEmpty(model.ApiKeys);
      Assert.Empty(model.ApiKeyScopes);
      Assert.False(model.CanCreateApiKey);
      Assert.Empty(model.PrivacySelections);
    }
    finally
    {
      user.TrySetResult(new UserResponse(new User("user-1", "alice")));
      await load;
    }

    Assert.All(model.ApiKeyScopes, scope => Assert.Equal("user", scope.ProtocolValue.Audience));
    Assert.NotEmpty(model.ApiKeyScopes);
    Assert.NotEmpty(model.PrivacySelections);
  }

  [Fact]
  public async Task ApiKeyCanBeCreatedWhileUnrelatedSettingsContentIsStillLoading()
  {
    var heldApiKeys = new TaskCompletionSource<ApiKeyListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var apiKeyCreation = new TaskCompletionSource<ApiKeyCreationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var apiKeyCreationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var catalogReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeSettingsService
    {
      FirstApiKeysPageTask = heldApiKeys.Task,
      ApiKeyCreationTask = apiKeyCreation.Task,
      ApiKeyCreationStarted = apiKeyCreationStarted,
    };
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader" };
    model.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(model.ApiKeyScopes) && model.ApiKeyScopes.Any(scope => scope.Scope == "rss:read"))
        catalogReady.TrySetResult();
    };
    var load = model.LoadAsync(TestContext.Current.CancellationToken);
    Task? create = null;
    try
    {
      await catalogReady.Task.WaitAsync(TestContext.Current.CancellationToken);
      model.SetApiKeyScopeSelected("rss:read", true);

      Assert.True(model.IsLoading);
      Assert.True(model.CanCreateApiKey);

      create = model.CreateApiKeyAsync(TestContext.Current.CancellationToken);
      await apiKeyCreationStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

      Assert.True(model.IsLoading);
      Assert.False(load.IsCompleted);
      Assert.False(create.IsCompleted);

      apiKeyCreation.TrySetResult(new ApiKeyCreationResponse(
          FakeSettingsService.CreateApiKey() with { Id = "created-key" },
          "raw-key"));
      await create;
      Assert.Contains(model.ApiKeys, apiKey => apiKey.Id == "created-key");
      Assert.Equal("Hello, Voucha!", model.ProfileMarkdown);
      Assert.False(model.IsLoading);
    }
    finally
    {
      heldApiKeys.TrySetResult(new ApiKeyListResponse([FakeSettingsService.CreateApiKey()], new PageInfo(null, false, null)));
      apiKeyCreation.TrySetResult(new ApiKeyCreationResponse(FakeSettingsService.CreateApiKey() with { Id = "created-key" }, "raw-key"));
      await load;
      if (create is not null) await create;
    }

    Assert.Equal("raw-key", model.ApiKeySecret);
    Assert.Contains(model.ApiKeys, apiKey => apiKey.Id == "created-key");
  }

  [Fact]
  public async Task OlderCredentialResponsesCannotOverwriteANewIdentityLoad()
  {
    var oldCatalog = new TaskCompletionSource<ScopeCatalogResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var oldGrants = new TaskCompletionSource<OAuthGrantListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeSettingsService
    {
      Roles = ["administrator"],
      CatalogTask = oldCatalog.Task,
      Grants = oldGrants.Task,
    };
    using var model = new SettingsViewModel(service) { ApiKeyType = "mcp" };
    var previousLoad = model.LoadAsync(TestContext.Current.CancellationToken);
    try
    {
      service.Roles = [];
      service.CatalogTask = Task.FromResult(SettingsCredentialTestFixtures.Catalog);
      service.Grants = Task.FromResult(SettingsCredentialTestFixtures.EmptyGrants);
      await model.LoadAsync(TestContext.Current.CancellationToken);

      oldCatalog.TrySetResult(new ScopeCatalogResponse([
        new("mcp.user:old", "user", "mcp", "read", ["api-key"], null, null),
      ]));
      oldGrants.TrySetResult(new OAuthGrantListResponse([Grant("old-account")], new PageInfo(null, false, null)));
      await previousLoad;

      Assert.Equal(["mcp.user:read", "mcp.user:write"], model.ApiKeyScopes.Select(scope => scope.Scope));
      Assert.Empty(model.OAuthGrants);
    }
    finally
    {
      oldCatalog.TrySetResult(SettingsCredentialTestFixtures.Catalog);
      oldGrants.TrySetResult(SettingsCredentialTestFixtures.EmptyGrants);
      await previousLoad;
    }
  }
}

using System.Reflection;
using System.ComponentModel;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class SettingsCredentialsRenderedControlsTests
{
  [Fact]
  public async Task RenderedScopeChecksApplyPrerequisitesAndSendOnlyExplicitUserChoices()
  {
    PrepareMaui();
    var service = DispatchProxy.Create<ISettingsService, CredentialSettingsService>();
    var fake = (CredentialSettingsService)service;
    fake.Catalog = new ScopeCatalogResponse([
        Scope("mcp.user:read"),
        Scope("mcp.user:write", "mcp.user:read"),
        Scope("mcp.user:financial:read"),
        Scope("mcp.admin:read", audience: "admin"),
        Scope("mcp.admin:write", audience: "user"),
    ]);
    using var model = new SettingsViewModel(service) { ApiKeyType = "mcp", ApiKeyLabel = "Scoped key" };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var view = LifecycleView(model);

    Assert.Equal(["mcp.user:read", "mcp.user:write", "mcp.user:financial:read"],
        model.ApiKeyScopes.Select(row => row.ProtocolValue.Scope));
    Assert.Empty(model.SelectedApiKeyScopes);
    var write = FindScopeCheck(view, "mcp.user:write");
    var changed = WhenScopeStateChanged(model);
    write.IsChecked = true;
    await changed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(["mcp.user:read", "mcp.user:write"], model.SelectedApiKeyScopes);

    var selectedView = LifecycleView(model);
    Assert.True(FindScopeCheck(selectedView, "mcp.user:read").IsChecked);
    Assert.True(FindScopeCheck(selectedView, "mcp.user:write").IsChecked);
    var financial = FindScopeCheck(selectedView, "mcp.user:financial:read");
    Assert.False(financial.IsChecked);
    changed = WhenScopeStateChanged(model);
    financial.IsChecked = true;
    await changed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(["mcp.user:read", "mcp.user:write", "mcp.user:financial:read"], model.SelectedApiKeyScopes);
    var create = Descendants<Button>(selectedView).Single(button => button.Text ==
        UiLocalization.English.Localize(UiMessageKey.NativeDotnetSettingsCreateApiKey));
    Assert.True(create.IsEnabled);
    var created = When(model, nameof(model.ApiKeySecret), () => model.ApiKeySecret == "secret");
    create.SendClicked();
    await created.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(["mcp.user:read", "mcp.user:write", "mcp.user:financial:read"], fake.CreatedPermissions);
    Assert.DoesNotContain(fake.CreatedPermissions, scope => scope.StartsWith("mcp.admin:", StringComparison.Ordinal));

    var reselectedView = LifecycleView(model);
    changed = WhenScopeStateChanged(model);
    FindScopeCheck(reselectedView, "mcp.user:write").IsChecked = true;
    await changed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(["mcp.user:read", "mcp.user:write"], model.SelectedApiKeyScopes);
    var removalView = LifecycleView(model);
    var read = FindScopeCheck(removalView, "mcp.user:read");
    Assert.True(read.IsChecked);
    changed = WhenScopeStateChanged(model);
    read.IsChecked = false;
    await changed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Empty(model.SelectedApiKeyScopes);
  }

  [Fact]
  public async Task RenderedGrantActionUsesPageHandlerAndRetainsGrantWhenRevocationFails()
  {
    PrepareMaui();
    var grant = Grant("grant-1");
    var service = DispatchProxy.Create<ISettingsService, CredentialSettingsService>();
    var fake = (CredentialSettingsService)service;
    fake.Grants = new OAuthGrantListResponse([grant], new PageInfo(null, false, null));
    var revoke = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fake.RevokeTask = revoke.Task;
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = new SettingsPage(model);
    Assert.Single(model.LocalizedOAuthGrants);
    var button = Descendants<Button>(page.ConnectedApps).Single(item => item.CommandParameter is SettingsOAuthGrantRow);
    Assert.Equal("grant-1", Assert.IsType<SettingsOAuthGrantRow>(button.CommandParameter).ProtocolValue.Id);
    var failureNotice = When(model, nameof(model.OAuthGrantNotice), () => model.OAuthGrantNotice is not null);
    button.SendClicked();

    await fake.RevokeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal("grant-1", fake.RevokedGrantId);
    Assert.Equal("grant-1", Assert.Single(model.OAuthGrants).Id);
    revoke.SetException(new VouchaApiException(System.Net.HttpStatusCode.Forbidden, "{}"));
    await failureNotice.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal("grant-1", Assert.Single(model.OAuthGrants).Id);
    Assert.NotNull(model.OAuthGrantNotice);

    fake.RevokeTask = Task.CompletedTask;
    var grantsChanged = When(model, nameof(model.OAuthGrants), () => model.OAuthGrants.Count == 0);
    var successNotice = When(model, nameof(model.OAuthGrantNotice), () => model.OAuthGrantNotice ==
        UiLocalization.English.Localize(UiMessageKey.NativeCredentialsGrantRevoked));
    button.SendClicked();
    await Task.WhenAll(grantsChanged, successNotice).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Empty(model.OAuthGrants);
  }

  [Fact]
  public async Task RenderedPaginationControlCallsPageHandlerWithTheOpaqueCursor()
  {
    PrepareMaui();
    var first = Grant("grant-1");
    var second = Grant("grant-2");
    var service = DispatchProxy.Create<ISettingsService, CredentialSettingsService>();
    var fake = (CredentialSettingsService)service;
    fake.Grants = new OAuthGrantListResponse([first], new PageInfo("opaque/+cursor", true, null));
    var nextPage = new TaskCompletionSource<OAuthGrantListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    fake.NextGrantPage = nextPage.Task;
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = new SettingsPage(model);
    var pagination = Descendants<Voucha.Client.App.Controls.HybridPaginationControl>(page)
        .Single(control => control.PaginationId == "settings-oauth-grants");
    Assert.True(pagination.HasMore);
    var pageChanged = When(model, nameof(model.OAuthGrants), () => model.OAuthGrants.Count == 2);
    Descendants<Button>(pagination).Single().SendClicked();

    await fake.NextGrantPageStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal("opaque/+cursor", fake.LastGrantCursor);
    nextPage.SetResult(new OAuthGrantListResponse([second], new PageInfo(null, false, null)));
    await pageChanged.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(["grant-1", "grant-2"], model.OAuthGrants.Select(item => item.Id));
  }

  private static ScopeCatalogEntry Scope(string name, string? requires = null, string audience = "user") =>
      new(name, audience, "mcp", name.EndsWith(":write", StringComparison.Ordinal) ? "write" : "read", ["api-key"], null, requires);

  private static CheckBox FindScopeCheck(VisualElement root, string scope) =>
      Descendants<CheckBox>(root).Single(check =>
          check.BindingContext is SettingsScopeRow row && row.ProtocolValue.Scope == scope);

  private static SettingsApiKeyLifecycleView LifecycleView(SettingsViewModel model)
  {
    var view = new SettingsApiKeyLifecycleView { BindingContext = model };
    _ = new ContentPage { Content = view };
    return view;
  }

  private static Task WhenScopeStateChanged(SettingsViewModel model)
  {
    var changed = new HashSet<string>(StringComparer.Ordinal);
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    PropertyChangedEventHandler? handler = null;
    handler = (_, args) =>
    {
      if (args.PropertyName is "ApiKeyScopes" or "SelectedApiKeyScopes" or "CanCreateApiKey")
        changed.Add(args.PropertyName);
      if (changed.Count == 3)
      {
        model.PropertyChanged -= handler;
        completion.TrySetResult();
      }
    };
    model.PropertyChanged += handler;
    return completion.Task;
  }

  private static OAuthGrant Grant(string id) => new(
      id,
      new OAuthGrantClient("client", "public-client", "Connected client", false),
      "https://voucha.ai/api/v1/mcp",
      ["mcp.user:read"],
      DateTimeOffset.UnixEpoch,
      null);

  private static void PrepareMaui()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    foreach (var key in UiMessageKey.All)
      application.Resources[key.Value] = UiLocalization.English.Localize(key);
  }

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static Task When(SettingsViewModel model, string property, Func<bool> condition)
  {
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    PropertyChangedEventHandler? handler = null;
    handler = (_, args) =>
    {
      if ((args.PropertyName is null || args.PropertyName == property) && condition())
      {
        model.PropertyChanged -= handler;
        completion.TrySetResult();
      }
    };
    model.PropertyChanged += handler;
    if (condition())
    {
      model.PropertyChanged -= handler;
      completion.TrySetResult();
    }
    return completion.Task;
  }

  public class CredentialSettingsService : DispatchProxy
  {
    public ScopeCatalogResponse Catalog { get; set; } = new([]);
    public OAuthGrantListResponse Grants { get; set; } = new([], new PageInfo(null, false, null));
    public Task<OAuthGrantListResponse>? NextGrantPage { get; set; }
    public Task RevokeTask { get; set; } = Task.CompletedTask;
    public TaskCompletionSource RevokeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource NextGrantPageStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlyList<string> CreatedPermissions { get; private set; } = [];
    public string? RevokedGrantId { get; private set; }
    public string? LastGrantCursor { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
      nameof(ISettingsService.FetchMyIdentityAsync) => Task.FromResult(new MyIdentityResponse(new User("user-1", "alice"))),
      nameof(ISettingsService.FetchUserAsync) => Task.FromResult(new UserResponse(new User("user-1", "alice"))),
      nameof(ISettingsService.FetchMyProfileAsync) => Task.FromResult(new MyProfileResponse(new MyProfile("profile-1", ""))),
      nameof(ISettingsService.FetchProfileLinksAsync) => Task.FromResult(new ProfileLinkListResponse([], EmptyPage())),
      nameof(ISettingsService.FetchApiKeysAsync) => Task.FromResult(new ApiKeyListResponse([], EmptyPage())),
      nameof(ISettingsService.FetchAuthSessionsAsync) => Task.FromResult(new AuthSessionListResponse([], EmptyPage())),
      nameof(ISettingsService.FetchMembershipPlansAsync) => Task.FromResult(new MembershipPlansResponse([])),
      nameof(ISettingsService.FetchMembershipAsync) => Task.FromResult<MembershipResponse?>(null),
      nameof(ISettingsService.FetchPushSubscriptionsAsync) => Task.FromResult(new WebPushSubscriptionListResponse([], EmptyPage())),
      nameof(ISettingsService.FetchUserDataRequestAsync) => Task.FromResult<UserDataRequestResponse?>(null),
      nameof(ISettingsService.FetchScopeCatalogAsync) => Task.FromResult(Catalog),
      nameof(ISettingsService.FetchOAuthGrantsAsync) => FetchGrants(args),
      nameof(ISettingsService.RevokeOAuthGrantAsync) => Revoke(args),
      nameof(ISettingsService.CreateApiKeyAsync) => CreateApiKey(args),
      _ => throw new NotSupportedException(targetMethod?.Name),
    };

    private Task<OAuthGrantListResponse> FetchGrants(object?[]? args)
    {
      LastGrantCursor = args?[0] as string;
      if (LastGrantCursor is null) return Task.FromResult(Grants);
      NextGrantPageStarted.TrySetResult();
      return NextGrantPage ?? Task.FromResult(new OAuthGrantListResponse([], EmptyPage()));
    }

    private Task Revoke(object?[]? args)
    {
      RevokedGrantId = args?[0] as string;
      RevokeStarted.TrySetResult();
      return RevokeTask;
    }

    private Task<ApiKeyCreationResponse> CreateApiKey(object?[]? args)
    {
      CreatedPermissions = Assert.IsAssignableFrom<IReadOnlyList<string>>(args?[2]);
      var key = new ApiKey("key", "user-1", "rk_key", "mcp", "Scoped key", CreatedPermissions,
          DateTimeOffset.UtcNow, null, null, DateTimeOffset.UtcNow);
      return Task.FromResult(new ApiKeyCreationResponse(key, "secret"));
    }

    private static PageInfo EmptyPage() => new(null, false, null);
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}

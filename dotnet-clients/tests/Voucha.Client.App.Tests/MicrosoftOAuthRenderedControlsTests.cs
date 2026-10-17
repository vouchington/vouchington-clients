using System.Net;
using System.Reflection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class MicrosoftOAuthRenderedControlsTests
{
  [Fact]
  public async Task GoogleControlsAreCapabilityGatedAndConnectTheGoogleProvider()
  {
    PrepareMaui();
    var disabled = new OAuthHandler(nativeEnabled: false);
    using var disabledCoordinator = Coordinator(disabled, new OAuthSessionStore());
    await disabledCoordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    var disabledPage = new AuthPage(disabledCoordinator);
    disabledPage.RefreshOAuthForTest();
    Assert.False(GoogleLoginButton(disabledPage).IsVisible);

    var enabled = new OAuthHandler(nativeEnabled: true);
    var persistence = new OAuthPersistence();
    using var coordinator = Coordinator(enabled, new OAuthSessionStore(), persistence);
    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    var loginPage = new AuthPage(coordinator);
    loginPage.RefreshOAuthForTest();
    var login = GoogleLoginButton(loginPage);
    Assert.True(login.IsVisible);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeTaxonomySettingsGoogle), login.Text);
    login.SendClicked();
    var loginPending = await persistence.Written.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(OAuthBrokerProvider.Google, loginPending.Provider);
    Assert.Equal(OAuthAuthorizationPurpose.Authenticate, loginPending.Purpose);
    Assert.Contains(enabled.Requests, request => request.Method == HttpMethod.Post &&
        request.Path == "/api/v1/auth/oauth/google/authorizations");

    var store = new OAuthSessionStore();
    var connectHandler = new OAuthHandler(nativeEnabled: true);
    var connectPersistence = new OAuthPersistence();
    using var connectCoordinator = Coordinator(connectHandler, store, connectPersistence);
    await connectCoordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    using var settingsModel = new SettingsViewModel(DispatchProxy.Create<ISettingsService, EmptySettingsService>());
    var settingsPage = new SettingsPage(settingsModel, store, connectCoordinator);
    settingsPage.RefreshOAuthForTest();
    var connect = GoogleSettingsButton(settingsPage);
    Assert.True(connect.IsVisible);
    connect.SendClicked();
    var connectPending = await connectPersistence.Written.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(OAuthBrokerProvider.Google, connectPending.Provider);
    Assert.Equal(OAuthAuthorizationPurpose.Connect, connectPending.Purpose);

    var connectedStore = new OAuthSessionStore(new SessionSnapshot(new User(
        "user-1", "testuser", GoogleAccount: new UserDisplayAccount("google-user", "Google User"))));
    var disconnectHandler = new OAuthHandler(nativeEnabled: false);
    using var disconnectCoordinator = Coordinator(disconnectHandler, connectedStore);
    await disconnectCoordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    var connectedPage = new SettingsPage(settingsModel, connectedStore, disconnectCoordinator);
    connectedPage.RefreshOAuthForTest();
    var disconnect = GoogleSettingsButton(connectedPage);
    Assert.True(disconnect.IsVisible);
    Assert.Contains(Descendants<Label>(connectedPage), label => label.Text == "Google User");
    var googleDisconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    connectedStore.SessionChanged += (_, change) =>
    {
      if (change.Current.Identity?.GoogleAccount is null) googleDisconnected.TrySetResult();
    };
    disconnect.SendClicked();
    var deleted = await disconnectHandler.Deleted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    await googleDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/auth/oauth/google/connect", deleted.Path);
    Assert.Null(connectedStore.Current.Identity?.GoogleAccount);
  }

  [Fact]
  public async Task LoginControlIsHiddenUntilAdvertisedAndStartsOnlyMicrosoftAuthorization()
  {
    PrepareMaui();
    var disabled = new OAuthHandler(nativeEnabled: false);
    using var disabledCoordinator = Coordinator(disabled, new OAuthSessionStore());
    await disabledCoordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    var disabledPage = new AuthPage(disabledCoordinator);
    disabledPage.RefreshOAuthForTest();
    Assert.False(MicrosoftLoginButton(disabledPage).IsVisible);

    var enabled = new OAuthHandler(nativeEnabled: true);
    var persistence = new OAuthPersistence();
    using var coordinator = Coordinator(enabled, new OAuthSessionStore(), persistence);
    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    var page = new AuthPage(coordinator);
    page.RefreshOAuthForTest();
    var button = MicrosoftLoginButton(page);
    Assert.True(button.IsVisible);
    Assert.True(button.IsEnabled);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeTaxonomySettingsMicrosoft), button.Text);

    button.SendClicked();
    var pending = await persistence.Written.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(OAuthBrokerProvider.Microsoft, pending.Provider);
    Assert.Equal(OAuthAuthorizationPurpose.Authenticate, pending.Purpose);
    Assert.Contains(enabled.Requests, request => request.Method == HttpMethod.Post &&
        request.Path == "/api/v1/auth/oauth/microsoft/authorizations");
    page.RefreshOAuthForTest();
    Assert.False(button.IsEnabled);
  }

  [Fact]
  public async Task SettingsControlShowsAdvertisedConnectAndConnectedIdentityDisconnect()
  {
    PrepareMaui();
    var disabledStore = new OAuthSessionStore();
    using var disabledCoordinator = Coordinator(new OAuthHandler(nativeEnabled: false), disabledStore);
    await disabledCoordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    using var settingsModel = new SettingsViewModel(DispatchProxy.Create<ISettingsService, EmptySettingsService>());
    var disabledPage = new SettingsPage(settingsModel, disabledStore, disabledCoordinator);
    disabledPage.RefreshOAuthForTest();
    Assert.False(MicrosoftSettingsButton(disabledPage).IsVisible);

    var enabled = new OAuthHandler(nativeEnabled: true);
    var persistence = new OAuthPersistence();
    var store = new OAuthSessionStore();
    using var coordinator = Coordinator(enabled, store, persistence);
    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    var page = new SettingsPage(settingsModel, store, coordinator);
    page.RefreshOAuthForTest();
    var button = MicrosoftSettingsButton(page);
    Assert.True(button.IsVisible);
    Assert.True(button.IsEnabled);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeSwiftSettingsBlueskyConnect), button.Text);
    button.SendClicked();
    var pending = await persistence.Written.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(OAuthBrokerProvider.Microsoft, pending.Provider);
    Assert.Equal(OAuthAuthorizationPurpose.Connect, pending.Purpose);
    Assert.Contains(enabled.Requests, request => request.Method == HttpMethod.Post &&
        request.Path == "/api/v1/auth/oauth/microsoft/authorizations");
    page.RefreshOAuthForTest();
    Assert.False(button.IsEnabled);

    var connectedStore = new OAuthSessionStore(new SessionSnapshot(new User(
        "user-1", "testuser", MicrosoftAccount: new UserDisplayAccount("microsoft-user", "Microsoft User"))));
    var connectedHandler = new OAuthHandler(nativeEnabled: false);
    using var connectedCoordinator = Coordinator(connectedHandler, connectedStore);
    await connectedCoordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    var connectedPage = new SettingsPage(settingsModel, connectedStore, connectedCoordinator);
    connectedPage.RefreshOAuthForTest();
    var disconnect = MicrosoftSettingsButton(connectedPage);
    Assert.True(disconnect.IsVisible);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeSwiftSettingsBlueskyDisconnect), disconnect.Text);
    Assert.Contains(Descendants<Label>(connectedPage), label => label.Text == "Microsoft User");
    disconnect.SendClicked();
    var deleted = await connectedHandler.Deleted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/auth/oauth/microsoft/connect", deleted.Path);
    Assert.Equal(HttpMethod.Delete, deleted.Method);
  }

  private static Button MicrosoftLoginButton(AuthPage page) =>
      Descendants<Button>(page).Single(button => button.Text ==
          UiLocalization.English.Localize(UiMessageKey.NativeTaxonomySettingsMicrosoft));

  private static Button MicrosoftSettingsButton(SettingsPage page) =>
      Descendants<Button>(page).Single(button => Equals(button.CommandParameter, "microsoft"));

  private static Button GoogleLoginButton(AuthPage page) =>
      Descendants<Button>(page).Single(button => button.Text ==
          UiLocalization.English.Localize(UiMessageKey.NativeTaxonomySettingsGoogle));

  private static Button GoogleSettingsButton(SettingsPage page) =>
      Descendants<Button>(page).Single(button => Equals(button.CommandParameter, "google"));

  private static IEnumerable<T> Descendants<T>(Element element) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)element).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static NativeOAuthAuthorizationCoordinator Coordinator(
      OAuthHandler handler,
      OAuthSessionStore store,
      OAuthPersistence? persistence = null)
  {
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") };
    return new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(httpClient),
        persistence ?? new OAuthPersistence(),
        new OAuthBrowser(),
        store,
        now: () => new DateTimeOffset(2027, 7, 29, 21, 55, 0, TimeSpan.Zero));
  }

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

  private sealed class OAuthHandler(bool nativeEnabled) : HttpMessageHandler
  {
    public List<(HttpMethod Method, string Path)> Requests { get; } = [];
    public TaskCompletionSource<(HttpMethod Method, string Path)> Deleted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      Requests.Add((request.Method, path));
      if (request.Method == HttpMethod.Delete) Deleted.TrySetResult((request.Method, path));
      var body = path switch
      {
        "/api/v1/auth/oauth/providers" => """
            {"providers":["facebook","x","github","linkedin","microsoft"],"broker_capabilities":{
              "facebook":{"version":1,"modes":{"web":false,"native":false},"purposes":["authenticate","connect"]},
              "x":{"version":1,"modes":{"web":false,"native":false},"purposes":["authenticate","connect"]},
              "github":{"version":1,"modes":{"web":false,"native":false},"purposes":["authenticate","connect"]},
              "microsoft":{"version":1,"modes":{"web":false,"native":NATIVE_ENABLED},"purposes":["authenticate","connect"]},
              "google":{"version":1,"modes":{"web":false,"native":NATIVE_ENABLED},"purposes":["authenticate","connect"]}
            }}
            """.Replace("NATIVE_ENABLED", nativeEnabled ? "true" : "false", StringComparison.Ordinal),
        "/api/v1/auth/oauth/microsoft/authorizations" => """
            {"flow_id":"019fafb8-a44c-73e2-890a-497ff3dd27a6","redirect_url":"https://login.microsoftonline.com/common/oauth2/v2.0/authorize","expires_at":"2027-07-29T22:00:00.000Z"}
            """,
        _ => "{}",
      };
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body),
      });
    }
  }

  private sealed class OAuthPersistence : INativeOAuthAuthorizationPersistence
  {
    public TaskCompletionSource<PendingNativeOAuthAuthorization> Written { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(CancellationToken token = default) =>
        Task.FromResult(new NativeOAuthAuthorizationSnapshot(null, null));
    public Task WritePendingAsync(PendingNativeOAuthAuthorization pending, CancellationToken token = default)
    {
      Written.TrySetResult(pending);
      return Task.CompletedTask;
    }
    public Task<bool> ClaimCallbackAsync(string flowId, string completionToken, CancellationToken token = default) =>
        Task.FromResult(false);
    public Task<bool> CompleteAsync(string flowId, NativeOAuthAuthorizationResult result, CancellationToken token = default) =>
        Task.FromResult(false);
    public Task ClearPendingAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task AcknowledgeResultAsync(CancellationToken token = default) => Task.CompletedTask;
  }

  private sealed class OAuthBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) => Task.FromResult(true);
  }

  private sealed class OAuthSessionStore(SessionSnapshot? initial = null) : ISessionStore, ICommittedOAuthDisconnectSessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = initial ?? SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void ApplyCommittedOAuthDisconnect(OAuthBrokerProvider provider)
    {
      Current = new SessionSnapshot(Current.Identity is { } identity ? provider switch
      {
        OAuthBrokerProvider.Microsoft => identity with { MicrosoftAccount = null },
        OAuthBrokerProvider.Google => identity with { GoogleAccount = null },
        _ => identity,
      } : null);
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }
  }

  public class EmptySettingsService : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
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

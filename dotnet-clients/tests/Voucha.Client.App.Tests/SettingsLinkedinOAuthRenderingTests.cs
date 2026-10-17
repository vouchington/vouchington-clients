using System.Net;
using System.Reflection;
using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class SettingsLinkedinOAuthRenderingTests
{
  [Theory]
  [InlineData(true, true)]
  [InlineData(false, false)]
  public async Task LinkedinAccountActionFollowsConnectCapability(bool enabled, bool expectedVisible)
  {
    PrepareMaui();
    var session = new TestSessionStore(SessionSnapshot.Anonymous);
    using var coordinator = Coordinator(enabled, session);
    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    using var model = new SettingsViewModel(DispatchProxy.Create<ISettingsService, SettingsServiceProxy>());
    var page = new SettingsPage(model, session, coordinator);

    Refresh(page);

    var button = VisualTreeElements.Descendants<Button>(page)
        .Single(item => Equals(item.CommandParameter, "linkedin"));
    Assert.Equal(expectedVisible, button.IsVisible);
    Assert.True(button.IsEnabled);
    if (expectedVisible)
    {
      Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeSwiftSettingsBlueskyConnect), button.Text);
    }
  }

  [Fact]
  public async Task ConnectedLinkedinIdentityRemainsVisibleWhenCapabilityIsDisabled()
  {
    PrepareMaui();
    var session = new TestSessionStore(new SessionSnapshot(new User(
        "user-1",
        "alice",
        LinkedinAccount: new UserDisplayAccount("linkedin-1", "LinkedIn Name"))));
    using var coordinator = Coordinator(enabled: false, session);
    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    using var model = new SettingsViewModel(DispatchProxy.Create<ISettingsService, SettingsServiceProxy>());
    var page = new SettingsPage(model, session, coordinator);

    Refresh(page);

    var button = VisualTreeElements.Descendants<Button>(page)
        .Single(item => Equals(item.CommandParameter, "linkedin"));
    var identity = Assert.IsType<Label>(typeof(SettingsPage)
        .GetField("LinkedinOAuthIdentity", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(page));
    Assert.True(button.IsVisible);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeSwiftSettingsBlueskyDisconnect), button.Text);
    Assert.Equal("LinkedIn Name", identity.Text);
  }

  [Fact]
  public async Task LinkedinAccountActionDisablesWhileAuthorizationIsInFlight()
  {
    PrepareMaui();
    var session = new TestSessionStore(SessionSnapshot.Anonymous);
    using var coordinator = Coordinator(enabled: true, session, includeStartResponse: true);
    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    await coordinator.StartAsync(
        OAuthBrokerProvider.Linkedin,
        OAuthAuthorizationPurpose.Connect,
        TestContext.Current.CancellationToken);
    using var model = new SettingsViewModel(DispatchProxy.Create<ISettingsService, SettingsServiceProxy>());
    var page = new SettingsPage(model, session, coordinator);

    Refresh(page);

    var button = VisualTreeElements.Descendants<Button>(page)
        .Single(item => Equals(item.CommandParameter, "linkedin"));
    Assert.True(button.IsVisible);
    Assert.False(button.IsEnabled);
  }

  private static NativeOAuthAuthorizationCoordinator Coordinator(
      bool enabled,
      ISessionStore session,
      bool includeStartResponse = false)
  {
    var linkedin = enabled
        ? "\"linkedin\":{\"version\":1,\"modes\":{\"native\":true,\"web\":false},\"purposes\":[\"connect\"]}"
        : "\"linkedin\":{\"version\":1,\"modes\":{\"native\":false,\"web\":false},\"purposes\":[]}";
    var json = "{\"providers\":[\"linkedin\"],\"broker_capabilities\":{" +
        "\"facebook\":{\"version\":1,\"modes\":{\"native\":false,\"web\":false},\"purposes\":[]}," +
        "\"x\":{\"version\":1,\"modes\":{\"native\":false,\"web\":false},\"purposes\":[]}," +
        "\"github\":{\"version\":1,\"modes\":{\"native\":false,\"web\":false},\"purposes\":[]}," + linkedin + "}}";
    return new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(new JsonHandler(json, includeStartResponse)) { BaseAddress = new Uri("https://api.test") }),
        new EmptyPersistence(),
        new RecordingBrowser(),
        session);
  }

  private static void Refresh(SettingsPage page) =>
      typeof(SettingsPage).GetMethod("RefreshOAuthAccountsPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(page, null);

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

  private sealed class JsonHandler(string json, bool includeStartResponse) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var body = includeStartResponse && request.Method == HttpMethod.Post
          ? "{\"flow_id\":\"019fafb8-a44c-73e2-890a-497ff3dd27a6\",\"redirect_url\":\"https://www.linkedin.com/oauth/v2/authorization\",\"expires_at\":\"2027-07-29T22:00:00.000Z\"}"
          : json;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
  }

  private sealed class TestSessionStore(SessionSnapshot current) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }
    public SessionSnapshot Current => current;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class EmptyPersistence : INativeOAuthAuthorizationPersistence
  {
    public PendingNativeOAuthAuthorization? Pending { get; private set; }
    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new NativeOAuthAuthorizationSnapshot(Pending, null));
    public Task WritePendingAsync(PendingNativeOAuthAuthorization pending, CancellationToken cancellationToken = default)
    {
      Pending = pending;
      return Task.CompletedTask;
    }
    public Task<bool> ClaimCallbackAsync(string flowId, string completionToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> CompleteAsync(string flowId, NativeOAuthAuthorizationResult result, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ClearPendingAsync(CancellationToken cancellationToken = default)
    {
      Pending = null;
      return Task.CompletedTask;
    }
    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class RecordingBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) => Task.FromResult(true);
  }

  private class SettingsServiceProxy : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
  }
}

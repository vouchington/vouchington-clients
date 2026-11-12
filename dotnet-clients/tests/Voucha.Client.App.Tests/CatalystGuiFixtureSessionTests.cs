#if DEBUG
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class NotificationPreferencesViewTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CatalystFixtureRefreshAuthenticatesTheNotificationRouteAndRendersItsHeading(bool restoredSession)
  {
    var origin = new Uri("https://voucha.test");
    var identityBody = ApiFixtureLoader.LoadResponse("swift.my.identity.default");
    var identity = JsonSerializer.Deserialize<MyIdentityResponse>(identityBody, VouchaApiJson.Options)!.Identity;
    var sessionBody = ApiFixtureLoader.LoadResponse("native.auth.session.refresh.rotated");
    var canonical = JsonSerializer.Deserialize<NativeSessionRefreshResponse>(sessionBody, VouchaApiJson.Options)!.Session;
    var preferences = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.my.email-preferences.default"))!.AsObject();
    var persistence = new GuiFixtureCookiePersistence();
    if (restoredSession)
    {
      using var previous = new SessionCookieJar(new CookieContainer(), origin, persistence);
      await previous.StoreSessionTokensAsync("previous-device", "previous-session", TimeSpan.FromHours(1),
          TimeSpan.FromHours(1), TestContext.Current.CancellationToken);
    }
    using var jar = new SessionCookieJar(new CookieContainer(), origin, persistence);
    using var http = new HttpClient(new ClientMetadataHandler(new(ClientPlatform.MacOS, "gui-test"), origin, jar,
        new SessionCookiePersistenceHandler(jar, new CatalystGuiFixtureHandler(
            new RejectGuiFixtureNetwork(), identityBody, preferences, sessionBody))))
    { BaseAddress = origin };
    var client = new VouchaApiClient(http);
    var store = new CookieSessionStore(client, jar);

    await ((ISessionStore)store).RefreshAsync(TestContext.Current.CancellationToken);

    Assert.True(store.Current.IsAuthenticated);
    Assert.Equal(identity.Id, store.Current.Identity!.Id);
    Assert.True(jar.CanRecoverAuthentication);
    Assert.Equal(canonical.Dt, jar.CookieContainer.GetCookies(origin)["dt"]!.Value);
    Assert.Equal(canonical.St, jar.CookieContainer.GetCookies(origin)["st"]!.Value);
    Assert.Contains(canonical.St, persistence.Value!);
    // A subsequent refresh must take the authenticated renewal path too.
    await ((ISessionStore)store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(store.Current.IsAuthenticated);
    Assert.True(jar.CanRecoverAuthentication);
    var route = NativeDeepLinkResolver.Resolve("/my/notification-settings",
        new NavigationViewer(store.Current.IsAuthenticated, [], IdentityId: store.Current.Identity!.Id));
    Assert.True(route.CanNavigate);
    Assert.Equal("/my/notification-settings", route.Match!.Path);

    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    using var model = new NotificationPreferencesViewModel(new ApiSettingsService(client));
    var view = new NotificationPreferencesView { BindingContext = model };
    var page = new ContentPage { Content = view };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();
    page.Measure(1024, 768);
    page.Arrange(new Rect(0, 0, 1024, 768));

    Assert.True(model.HasLoadedPreferences);
    Assert.True(Find<Label>(view, "notification-preferences-heading").IsVisible);
    Assert.True(Find<Switch>(view, "notification-engagement").IsEnabled);
  }

  private sealed class RejectGuiFixtureNetwork : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A GUI fixture request must not reach the network.");
  }

  private sealed class GuiFixtureCookiePersistence : ISessionCookiePersistence
  {
    public string? Value { get; private set; }
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
    public Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default)
    {
      Value = serializedCookies;
      return Task.CompletedTask;
    }
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
      Value = null;
      return Task.CompletedTask;
    }
  }
}
#endif

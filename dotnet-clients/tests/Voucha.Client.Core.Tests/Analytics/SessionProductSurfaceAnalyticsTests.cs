using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Analytics;

public sealed class SessionProductSurfaceAnalyticsTests
{
  [Fact]
  public async Task VisitAndClickUseTheSessionClientAndAFailedBeaconIsNotRetried()
  {
    var handler = new RecordingHandler { Status = HttpStatusCode.InternalServerError };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var analytics = new SessionProductSurfaceAnalytics(new VouchaApiClient(http), SignedInSession());

    analytics.RecordLandingPageVisit("page-1", new LandingPageVisitSignal(UtmSource: "newsletter", UtmMedium: "email"));
    await analytics.LastDispatch;
    analytics.RecordLandingPageClick("page-1", new LandingPageClickSignal("item-1", "member-1"));
    await analytics.LastDispatch;
    analytics.RecordLandingPageClick("page-1", new LandingPageClickSignal("item-1"));
    await analytics.LastDispatch;

    Assert.Equal(
        [
            "/api/v1/landing-pages/page-1/visits",
            "/api/v1/landing-pages/page-1/clicks",
            "/api/v1/landing-pages/page-1/clicks"
        ],
        handler.Paths);
    Assert.Equal(HttpMethod.Post, handler.Methods[0]);
    Assert.Null(handler.Authorization[0]);
    using var visit = JsonDocument.Parse(handler.Bodies[0]!);
    Assert.Equal("newsletter", visit.RootElement.GetProperty("utm_source").GetString());
    Assert.Equal("email", visit.RootElement.GetProperty("utm_medium").GetString());
    Assert.False(visit.RootElement.TryGetProperty("referrer", out _));
    using var click = JsonDocument.Parse(handler.Bodies[1]!);
    Assert.Equal("item-1", click.RootElement.GetProperty("landing_page_item_id").GetString());
    Assert.Equal("member-1", click.RootElement.GetProperty("group_member_id").GetString());
  }

  [Fact]
  public async Task SignedOutSessionDoesNotSendVisitClickOrRssSignals()
  {
    var handler = new RecordingHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var analytics = new SessionProductSurfaceAnalytics(new VouchaApiClient(http), new AnonymousSessionStore());

    analytics.RecordLandingPageVisit("page-1", new LandingPageVisitSignal(UtmSource: "newsletter"));
    await analytics.LastDispatch;
    analytics.RecordLandingPageClick("page-1", new LandingPageClickSignal("item-1"));
    await analytics.LastDispatch;
    analytics.RecordRssFeedView("feed-1");
    await analytics.LastDispatch;
    analytics.RecordRssFeedItemView("item-1");
    await analytics.LastDispatch;

    Assert.Empty(handler.Paths);
  }

  [Fact]
  public async Task ASignInAfterConstructionSendsTheNextSignal()
  {
    var handler = new RecordingHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var session = new MutableSessionStore();
    var analytics = new SessionProductSurfaceAnalytics(new VouchaApiClient(http), session);

    analytics.RecordLandingPageVisit("page-1", new LandingPageVisitSignal(UtmSource: "newsletter"));
    await analytics.LastDispatch;
    Assert.Empty(handler.Paths);

    session.Current = new SessionSnapshot(new User("account-1", "ada"));
    analytics.RecordRssFeedView("feed-1");
    await analytics.LastDispatch;

    Assert.Equal(["/api/v1/rss-feeds/feed-1/views"], handler.Paths);
  }

  [Fact]
  public async Task RssViewsPostOncePerCallWithNoBody()
  {
    var handler = new RecordingHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var analytics = new SessionProductSurfaceAnalytics(new VouchaApiClient(http), SignedInSession());

    analytics.RecordRssFeedView("feed-1");
    await analytics.LastDispatch;
    analytics.RecordRssFeedItemView("item-1");
    await analytics.LastDispatch;

    Assert.Equal(["/api/v1/rss-feeds/feed-1/views", "/api/v1/rss-feed-items/item-1/views"], handler.Paths);
    Assert.All(handler.Bodies, Assert.Null);
    Assert.All(handler.Methods, method => Assert.Equal(HttpMethod.Post, method));
  }

  private static ISessionStore SignedInSession() =>
      new FixedSessionStore(new SessionSnapshot(new User("account-1", "ada")));

  private sealed class MutableSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; set; } = SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class FixedSessionStore(SessionSnapshot snapshot) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; } = snapshot;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class RecordingHandler : HttpMessageHandler
  {
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public List<string> Paths { get; } = [];

    public List<HttpMethod> Methods { get; } = [];

    public List<string?> Bodies { get; } = [];

    public List<string?> Authorization { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.AbsolutePath);
      Methods.Add(request.Method);
      Authorization.Add(request.Headers.Authorization?.ToString());
      Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
      return new HttpResponseMessage(Status)
      {
        Content = new StringContent("""{"ok":true}""")
      };
    }
  }
}

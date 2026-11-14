using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class PublicLandingPageViewModelTests
{
  [Theory]
  [InlineData(null, "/api/v1/users/alice%20name/landing-page")]
  [InlineData("travel links", "/api/v1/users/alice%20name/landing-pages/travel%20links")]
  public async Task PublicGetUsesCanonicalEnvelopeWithoutOwnerItemLimit(string? slug, string path)
  {
    var handler = new PublicHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new("https://api.test") });
    var result = await client.FetchPublicLandingPageAsync("alice name", slug, TestContext.Current.CancellationToken);
    Assert.Equal(path, handler.Path);
    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal("Alice", result.User.DisplayName);
    Assert.Equal(5, result.LandingPage.Items?.Count);
    var missing = JsonNode.Parse(PublicLandingPageTestData.PublicUnitJson())!.AsObject();
    missing.Remove("user");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PublicLandingPageResponse>(missing.ToJsonString(), VouchaApiJson.Options));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void PublicUserAllowsMissingOrNullUsername(bool explicitNull)
  {
    var response = JsonNode.Parse(PublicLandingPageTestData.PublicUnitJson())!.AsObject();
    var user = response["user"]!.AsObject();
    if (explicitNull) user["username"] = null;
    else user.Remove("username");
    var decoded = JsonSerializer.Deserialize<PublicLandingPageResponse>(response.ToJsonString(), VouchaApiJson.Options)!;
    Assert.Null(decoded.User.Username);
    Assert.Equal("user-abc", decoded.User.Id);
    Assert.Equal("Alice", decoded.User.DisplayName);
    Assert.Equal(5, decoded.LandingPage.Items?.Count);
  }

  [Theory]
  [InlineData("user", false)]
  [InlineData("user", true)]
  [InlineData("landing_page", false)]
  [InlineData("landing_page", true)]
  [InlineData("items", false)]
  [InlineData("items", true)]
  public void PublicEnvelopeRejectsMissingOrNullUserPageAndItems(string field, bool explicitNull)
  {
    var response = JsonNode.Parse(PublicLandingPageTestData.PublicUnitJson())!.AsObject();
    var owner = field == "items" ? response["landing_page"]!.AsObject() : response;
    if (explicitNull) owner[field] = null;
    else owner.Remove(field);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PublicLandingPageResponse>(response.ToJsonString(), VouchaApiJson.Options));
  }

  [Theory]
  [InlineData("/landing/alice")]
  [InlineData("/landing/alice/travel")]
  [InlineData("/user/alice/landing")]
  [InlineData("/@alice")]
  [InlineData("/@alice/travel")]
  public void PublicRoutesAreVisibleSignedOutAndDoNotSelectOwnerEditor(string path)
  {
    var route = NativeDeepLinkResolver.Resolve(path, new NavigationViewer(false, []));
    Assert.True(route.CanNavigate);
    Assert.False(route.RequiresAuthentication);
    Assert.True(LandingPagesDeepLinkPolicy.TryGetPublicTarget(route.Match, out var username, out _));
    Assert.Equal("alice", username);
    Assert.False(LandingPagesDeepLinkPolicy.TryGetOwnerManagementSlug(route, out _));
  }

  [Fact]
  public async Task PublicRouteAttributionStringsCannotEnterProductVisitRequest()
  {
    var route = NativeDeepLinkResolver.Resolve(
        "/@alice?ref=alice%40example.com&utm_source=alice%40example.com&utm_medium=192.0.2.1&utm_campaign=Alice%20Example&utm_content=private-user",
        new NavigationViewer(true, []));
    Assert.True(LandingPagesDeepLinkPolicy.TryGetPublicTarget(route.Match, out var username, out var slug));
    var handler = new PublicHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new("https://api.test") });
    var session = new PublicLandingSession();
    var analytics = new SessionProductSurfaceAnalytics(client, session);
    using var model = new PublicLandingPageViewModel(token => client.FetchPublicLandingPageAsync(username!, slug, token), session, analytics);
    await model.ActivateAsync(TestContext.Current.CancellationToken);
    await analytics.LastDispatch.WaitAsync(TestContext.Current.CancellationToken);
    var visit = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    Assert.Equal("/api/v1/landing-pages/landing-page-1/visits", visit.Path);
    using var body = JsonDocument.Parse(visit.Body!);
    Assert.Empty(body.RootElement.EnumerateObject());
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task VisitOncePerActivationAndClicksUseParentAndMemberIdentity()
  {
    var session = new PublicLandingSession();
    var signals = new PublicLandingSignals();
    using var model = new PublicLandingPageViewModel(_ => Task.FromResult(PublicLandingPageTestData.Response()), session, signals);
    await model.ActivateAsync(TestContext.Current.CancellationToken);
    await model.ActivateAsync(TestContext.Current.CancellationToken);
    await model.ReloadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("landing-page-1", Assert.Single(signals.Visits));
    Assert.Null(model.ActivateItem("item-topic-group-1"));
    Assert.Empty(signals.Clicks);
    Assert.Equal("travel-card-benefits", model.ActivateItem("item-topic-group-1", "group-entry-review-1")?.ReviewIdOrSlug);
    Assert.Equal("https://example.com/travel-card", model.ActivateItem("item-topic-group-1", "group-entry-referral-1")?.ExternalUrl?.ToString());
    Assert.Equal("best-travel-card", model.ActivateItem("item-review-1")?.ReviewIdOrSlug);
    Assert.Equal("https://example.com/", model.ActivateItem("item-profile-1")?.ExternalUrl?.ToString());
    Assert.Equal("https://example.com/newsletter", model.ActivateItem("item-link-1")?.ExternalUrl?.ToString());
    Assert.Equal(new LandingPageClickSignal("item-topic-group-1", "group-entry-review-1"), signals.Clicks[0].Signal);
    Assert.Null(signals.Clicks[2].Signal.GroupMemberId);
    Assert.Equal(5, signals.Clicks.Count);
    model.Deactivate();
    Assert.Null(model.ActivateItem("item-link-1"));
    await model.ActivateAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, signals.Visits.Count);
  }

  [Fact]
  public async Task SignedOutStillLoadsAndNavigatesWithoutSignals()
  {
    var session = new PublicLandingSession();
    session.Set(null);
    var signals = new PublicLandingSignals();
    using var model = new PublicLandingPageViewModel(_ => Task.FromResult(PublicLandingPageTestData.Response()), session, signals);
    await model.ActivateAsync(TestContext.Current.CancellationToken);
    Assert.Equal("My Links", model.Page?.Title);
    Assert.NotNull(model.ActivateItem("item-link-1"));
    Assert.Empty(signals.Visits);
    Assert.Empty(signals.Clicks);
  }

  [Fact]
  public async Task AccountChangeReloadsVisibleContentAndDiscardsPendingOldResponse()
  {
    var session = new PublicLandingSession();
    var signals = new PublicLandingSignals();
    var pending = new TaskCompletionSource<PublicLandingPageResponse>();
    var loads = 0;
    using var model = new PublicLandingPageViewModel(_ => ++loads == 1 ? pending.Task
        : Task.FromResult(PublicLandingPageTestData.Response() with
        {
          LandingPage = PublicLandingPageTestData.Response().LandingPage with { Title = "Current account content" },
        }), session, signals);
    var load = model.ActivateAsync(TestContext.Current.CancellationToken);
    session.Set("viewer-2");
    Assert.Equal("Current account content", model.Page?.Title);
    pending.SetResult(PublicLandingPageTestData.Response());
    await load;
    Assert.Equal("Current account content", model.Page?.Title);
    Assert.Single(signals.Visits);
    session.Set(null);
    Assert.Equal("Current account content", model.Page?.Title);
    Assert.NotNull(model.ActivateItem("item-link-1"));
    Assert.Empty(signals.Clicks);
    Assert.Single(signals.Visits);
    model.Deactivate();
    await model.ReloadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(3, loads);
  }

  [Fact]
  public async Task FailedLoadCanRetryWithoutBlockingOrDuplicatingVisit()
  {
    var fail = true;
    var signals = new PublicLandingSignals();
    using var model = new PublicLandingPageViewModel(_ => fail
        ? Task.FromException<PublicLandingPageResponse>(new HttpRequestException("offline"))
        : Task.FromResult(PublicLandingPageTestData.Response()), new PublicLandingSession(), signals);
    await model.ActivateAsync(TestContext.Current.CancellationToken);
    Assert.Equal("offline", model.Error?.VerbatimValue);
    Assert.False(model.IsLoading);
    fail = false;
    await model.ReloadAsync(TestContext.Current.CancellationToken);
    Assert.Null(model.Error);
    Assert.Single(signals.Visits);
  }

  [Fact]
  public async Task DisposedViewerCancelsLoadAndCannotReactivateOrPublishOldContent()
  {
    var session = new PublicLandingSession();
    var signals = new PublicLandingSignals();
    var response = new TaskCompletionSource<PublicLandingPageResponse>();
    var loads = 0;
    CancellationToken pendingToken = default;
    var model = new PublicLandingPageViewModel(token =>
    {
      loads++;
      pendingToken = token;
      return response.Task;
    }, session, signals);
    var pending = model.ActivateAsync(TestContext.Current.CancellationToken);
    model.Dispose();
    model.Dispose();
    Assert.True(pendingToken.IsCancellationRequested);
    session.Set("other-viewer");
    response.SetResult(PublicLandingPageTestData.Response());
    await pending;
    await model.ActivateAsync(TestContext.Current.CancellationToken);
    await model.ReloadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(1, loads);
    Assert.Null(model.Page);
    Assert.False(model.IsLoading);
    Assert.Empty(signals.Visits);
    Assert.Empty(signals.Clicks);
  }

  [Theory]
  [InlineData("twitter", "https://x.com/alice")]
  [InlineData("facebook", "https://www.facebook.com/alice")]
  [InlineData("instagram", "https://www.instagram.com/alice")]
  [InlineData("github", "https://github.com/alice")]
  [InlineData("linkedin", "https://www.linkedin.com/in/alice")]
  [InlineData("youtube", "https://www.youtube.com/@alice")]
  [InlineData("tiktok", "https://www.tiktok.com/@alice")]
  public void SocialProfileHandlesUseTheirPublicServiceTargets(string type, string expected)
  {
    var profile = Assert.IsType<LandingPageProfileLinkItem>(PublicLandingPageTestData.Response().LandingPage.Items![0]);
    var item = profile with { ProfileLink = profile.ProfileLink with { LinkType = type, Handle = "@alice" } };
    Assert.Equal(expected, PublicLandingPageTarget.For(item)?.ExternalUrl?.ToString());
  }

  [Fact]
  public void BlankReviewSlugAndProfileLabelsUseNonemptyNativeTargets()
  {
    var page = PublicLandingPageTestData.Response().LandingPage;
    var review = Assert.IsType<LandingPageReviewItem>(page.Items![1]);
    Assert.Equal("review-1", PublicLandingPageTarget.For(review with { Review = review.Review with { Slug = "  " } })?.ReviewIdOrSlug);
    var profile = Assert.IsType<LandingPageProfileLinkItem>(page.Items[0]);
    var unnamed = profile with { ProfileLink = profile.ProfileLink with { Name = "  ", Handle = null } };
    Assert.Equal("https://example.com/", PublicLandingPageTarget.Label(unnamed).VerbatimValue);
  }

  [Fact]
  public async Task UnsafeOrUnknownTargetsDoNotNavigateOrEmitClicks()
  {
    var signals = new PublicLandingSignals();
    var response = PublicLandingPageTestData.Response();
    response = response with
    {
      LandingPage = response.LandingPage with
      {
        Items = [new LandingPageLinkItem("unsafe", "Unsafe", new Uri("javascript:alert(1)"))],
      }
    };
    using var model = new PublicLandingPageViewModel(_ => Task.FromResult(response), new PublicLandingSession(), signals);
    await model.ActivateAsync(TestContext.Current.CancellationToken);
    Assert.Null(model.ActivateItem("unsafe"));
    Assert.Null(model.ActivateItem("unknown"));
    Assert.Empty(signals.Clicks);
  }

  private sealed class PublicHandler : HttpMessageHandler
  {
    public string? Path { get; private set; }
    public HttpMethod? Method { get; private set; }
    public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Path = request.RequestUri!.AbsolutePath;
      Method = request.Method;
      Requests.Add((request.Method, request.RequestUri.PathAndQuery,
          request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PublicLandingPageTestData.PublicUnitJson()) };
    }
  }
}

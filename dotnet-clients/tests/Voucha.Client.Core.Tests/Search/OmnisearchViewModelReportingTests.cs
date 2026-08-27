using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed class OmnisearchViewModelReportingTests
{
  [Theory]
  [InlineData("domain")]
  [InlineData("url")]
  [InlineData("crawls")]
  [InlineData("crawl")]
  public async Task DomainAndUrlRoutesUseCanonicalHostnameReportTarget(string route)
  {
    var responses = RouteResponses(route).Append(new RecordedResponse("{}"));
    var handler = new RecordingHandler(responses);
    var viewModel = ViewModel(handler, authenticated: true);

    await LoadRouteAsync(viewModel, route);
    var submitted = await viewModel.ReportSelectedHostnameAsync(
        "spam",
        "  useful context  ",
        "turnstile-token",
        TestContext.Current.CancellationToken);

    var request = Assert.Single(handler.Requests, request => request.PathAndQuery == "/api/v1/reports");
    Assert.Contains("\"entityType\":\"url_hostname\"", request.Body, StringComparison.Ordinal);
    Assert.Contains("\"entityId\":\"hostname-1\"", request.Body, StringComparison.Ordinal);
    Assert.Contains("\"note\":\"useful context\"", request.Body, StringComparison.Ordinal);
    Assert.Contains("\"cf_turnstile_response\":\"turnstile-token\"", request.Body, StringComparison.Ordinal);
    Assert.True(submitted);
    Assert.Equal(OmnisearchReportSubmissionState.Submitted, viewModel.ReportSubmissionState);
    Assert.Equal(
        UiMessageKey.NativeSwiftModerationReportsReported,
        viewModel.ReportButtonText.Key);
  }

  [Fact]
  public async Task RouteContextLoadsUrlCrawlDetail()
  {
    var routeContextStore = new OmnisearchWebSearchRouteContextStore();
    routeContextStore.Set(new(OmnisearchWebSearchSurface.Urls, "url-1", "crawl-1"));
    var handler = new RecordingHandler([
      new RecordedResponse(UrlCrawlDetailJson),
      new RecordedResponse(UrlJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, routeContextStore);

    await viewModel.LoadRouteContextAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal("/api/v1/urls/url-1/crawls/crawl-1", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/urls/url-1", handler.Requests[1].PathAndQuery);
    Assert.Equal(OmnisearchWebSearchSurface.Urls, viewModel.ActiveSurface);
    Assert.True(viewModel.HasSelectedUrl);
    Assert.Equal("Crawl detail", viewModel.Groups[0].Title);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(true, true)]
  public async Task BlockedAndSignedOutHostnamesCannotBeReported(bool authenticated, bool blocked)
  {
    var handler = new RecordingHandler(DomainJson(blocked));
    var viewModel = ViewModel(handler, authenticated);

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    var submitted = await viewModel.ReportSelectedHostnameAsync(
        "spam",
        null,
        "token",
        TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasSelectedReportTarget);
    Assert.False(viewModel.CanReportSelectedHostname);
    Assert.False(submitted);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task SubmissionPreventsConcurrentDuplicateRequests()
  {
    var handler = new DeferredReportHandler();
    var viewModel = ViewModel(handler, authenticated: true);
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    var first = viewModel.ReportSelectedHostnameAsync("spam", null, "token-1", TestContext.Current.CancellationToken);
    await handler.ReportStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    var duplicate = await viewModel.ReportSelectedHostnameAsync(
        "other",
        null,
        "token-2",
        TestContext.Current.CancellationToken);
    handler.CompleteReport();
    var submitted = await first;

    Assert.Equal(1, handler.ReportRequestCount);
    Assert.False(duplicate);
    Assert.True(submitted);
    Assert.Equal(OmnisearchReportSubmissionState.Submitted, viewModel.ReportSubmissionState);
  }

  [Fact]
  public async Task InFlightReportCompletionDoesNotOverwriteReloadedDetailState()
  {
    var handler = new DeferredReportHandler();
    var viewModel = ViewModel(handler, authenticated: true);
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    var report = viewModel.ReportSelectedHostnameAsync("spam", null, "token", TestContext.Current.CancellationToken);
    await handler.ReportStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadDomainDetailAsync("hostname-2", TestContext.Current.CancellationToken);
    handler.CompleteReport();
    var submitted = await report;

    Assert.False(submitted);
    Assert.Equal(OmnisearchReportSubmissionState.Idle, viewModel.ReportSubmissionState);
    Assert.Equal(
        UiMessageKey.NativeSwiftModerationReportsReport,
        viewModel.ReportButtonText.Key);
    Assert.True(viewModel.CanReportSelectedHostname);
  }

  [Fact]
  public async Task FailureIsRetryableWithFreshTurnstileToken()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(DomainJson(false)),
      new RecordedResponse("{}", HttpStatusCode.UnprocessableEntity),
      new RecordedResponse("{}"),
    ]);
    var viewModel = ViewModel(handler, authenticated: true);
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<VouchaApiException>(() => viewModel.ReportSelectedHostnameAsync(
        "misinformation",
        "context",
        "spent-token",
        TestContext.Current.CancellationToken));
    Assert.Equal(OmnisearchReportSubmissionState.Idle, viewModel.ReportSubmissionState);

    await viewModel.ReportSelectedHostnameAsync(
        "misinformation",
        "context",
        "fresh-token",
        TestContext.Current.CancellationToken);

    Assert.Contains("fresh-token", handler.Requests[^1].Body, StringComparison.Ordinal);
    Assert.Equal(OmnisearchReportSubmissionState.Submitted, viewModel.ReportSubmissionState);
  }

  [Fact]
  public void LocaleChangeRefreshesReportButtonPresentation()
  {
    var controller = new UiLocaleController(new ReportLanguageProvider());
    var localization = new UiLocalization(controller);
    using var viewModel = new OmnisearchViewModel(
        new VouchaApiClient(
            new HttpClient(new RecordingHandler([])) { BaseAddress = new Uri("https://api.test") }),
        localization: localization,
        localeController: controller);
    var changedProperties = new List<string?>();
    viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

    Assert.Equal("Report", localization.Resolve(viewModel.ReportButtonText));

    controller.ApplySavedLocale("es");

    Assert.Equal("Denunciar", localization.Resolve(viewModel.ReportButtonText));
    Assert.Contains(nameof(OmnisearchViewModel.ReportButtonText), changedProperties);
  }

  [Fact]
  public async Task NetworkFailureIsRetryableWithFreshTurnstileToken()
  {
    var handler = new NetworkThenSuccessHandler();
    var viewModel = ViewModel(handler, authenticated: true);
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<HttpRequestException>(() => viewModel.ReportSelectedHostnameAsync(
        "spam",
        "context",
        "spent-token",
        TestContext.Current.CancellationToken));
    Assert.Equal(OmnisearchReportSubmissionState.Idle, viewModel.ReportSubmissionState);

    await viewModel.ReportSelectedHostnameAsync(
        "spam",
        "context",
        "fresh-token",
        TestContext.Current.CancellationToken);

    Assert.Equal(2, handler.ReportRequestCount);
    Assert.Contains("fresh-token", handler.LastReportBody, StringComparison.Ordinal);
    Assert.Equal(OmnisearchReportSubmissionState.Submitted, viewModel.ReportSubmissionState);
  }

  [Fact]
  public async Task ReloadResetsReportStateAndUrlLookupFailureDoesNotHideCrawls()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(DomainJson(false)),
      new RecordedResponse("{}"),
      new RecordedResponse(CrawlsJson),
      new RecordedResponse("{}", HttpStatusCode.InternalServerError),
    ]);
    var viewModel = ViewModel(handler, authenticated: true);
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    await viewModel.ReportSelectedHostnameAsync("spam", null, "token", TestContext.Current.CancellationToken);

    await viewModel.LoadUrlCrawlsAsync("url-1", TestContext.Current.CancellationToken);

    Assert.Equal(OmnisearchReportSubmissionState.Idle, viewModel.ReportSubmissionState);
    Assert.False(viewModel.HasSelectedReportTarget);
    Assert.Contains(viewModel.Groups.SelectMany(group => group.Rows), row => row.Title == "Crawl title");
  }

  private static OmnisearchViewModel ViewModel(HttpMessageHandler handler, bool authenticated)
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(authenticated ? new NavigationViewer(true, []) : NavigationViewer.Anonymous);
    return new OmnisearchViewModel(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        viewerProvider: viewerProvider);
  }

  private static Task LoadRouteAsync(OmnisearchViewModel viewModel, string route) => route switch
  {
    "domain" => viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken),
    "url" => viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken),
    "crawls" => viewModel.LoadUrlCrawlsAsync("url-1", TestContext.Current.CancellationToken),
    "crawl" => viewModel.LoadUrlCrawlDetailAsync("url-1", "crawl-1", TestContext.Current.CancellationToken),
    _ => throw new ArgumentOutOfRangeException(nameof(route)),
  };

  private static IEnumerable<RecordedResponse> RouteResponses(string route) => route switch
  {
    "domain" => [new RecordedResponse(DomainJson(false))],
    "url" => [new RecordedResponse(UrlJson)],
    "crawls" => [new RecordedResponse(CrawlsJson), new RecordedResponse(UrlJson)],
    "crawl" => [new RecordedResponse(UrlCrawlDetailJson), new RecordedResponse(UrlJson)],
    _ => throw new ArgumentOutOfRangeException(nameof(route)),
  };

  private static string DomainJson(bool blocked) => $$"""
      {
        "hostname": { "id": "hostname-1", "hostname": "example.com", "blocked": {{blocked.ToString().ToLowerInvariant()}} },
        "topic": null, "top_urls": [], "rss_feeds": [], "hostname_election": null, "election_vote": null
      }
      """;

  private const string UrlJson = """
      {
        "url": { "id": "url-1", "url": "https://example.com/a", "pathname": "/a",
          "hostname": { "id": "hostname-1", "hostname": "example.com", "blocked": false } },
        "latest_crawl": null, "can_view_latest_crawl": true, "can_view_crawl_history": true,
        "can_trigger_crawl": false, "url_type": "web", "rss_feed_id": null
      }
      """;

  private const string CrawlsJson = """
      { "results": [{ "id": "crawl-1", "url_id": "url-1", "created_at": "2026-01-01T00:00:00Z",
        "completed_at": "2026-01-01T00:01:00Z", "response_status_code": 200, "title": "Crawl title" }],
        "page_info": { "has_next_page": false } }
      """;

  private const string UrlCrawlDetailJson = """
      {
        "crawl": {
          "__entity_type": "crawl",
          "id": "crawl-1",
          "url_id": "url-1",
          "created_at": "2026-07-01T00:00:00Z",
          "completed_at": "2026-07-01T00:01:00Z",
          "response_status_code": 200,
          "title": "Native result",
          "markdown": "# Native result",
          "lang": "en"
        },
        "og_image_sideload": null
      }
      """;

  private sealed class DeferredReportHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource reportCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource ReportStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ReportRequestCount { get; private set; }

    public void CompleteReport() => reportCompletion.TrySetResult();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      if (request.RequestUri?.AbsolutePath == "/api/v1/reports")
      {
        ReportRequestCount++;
        ReportStarted.TrySetResult();
        await reportCompletion.Task.WaitAsync(cancellationToken);
        return Json("{}");
      }

      return Json(DomainJson(false));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    };
  }

  private sealed class NetworkThenSuccessHandler : HttpMessageHandler
  {
    public int ReportRequestCount { get; private set; }

    public string? LastReportBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      if (request.RequestUri?.AbsolutePath != "/api/v1/reports")
      {
        return Json(DomainJson(false));
      }

      ReportRequestCount++;
      LastReportBody = request.Content is null
          ? null
          : await request.Content.ReadAsStringAsync(cancellationToken);
      if (ReportRequestCount == 1)
      {
        throw new HttpRequestException("Network unavailable");
      }

      return Json("{}");
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    };
  }

  private sealed class ReportLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }
}

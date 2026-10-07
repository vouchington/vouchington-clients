using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  [Fact]
  public async Task SearchAsyncCallsCombinedSearchAndBuildsGroups()
  {
    var handler = new RecordingHandler(CombinedSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client) { Query = "native swift" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.StartsWith("/api/v1/search?", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.Contains("q=native%20swift", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.Contains("limit=10", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.Equal(["Topics", "Posts", "News", "Domains", "Communities"], viewModel.Groups.Select(group => group.Title));
    Assert.Equal("Topic: Native Swift", viewModel.Groups[0].Rows[0].Title);
    Assert.Equal(
        ["Community: Saved Club", "Community: Alpha Club", "Community: Open Club"],
        viewModel.Groups[^1].Rows.Select(row => row.Title));
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task LocaleChangeRemapsVisibleSearchRowsWithoutAnotherRequest()
  {
    var handler = new RecordingHandler(CombinedSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    using var viewModel = new OmnisearchViewModel(
        client,
        localization: new UiLocalization(controller),
        localeController: controller)
    {
      Query = "native",
    };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);
    controller.ApplySavedLocale("es");

    Assert.Equal("Temas", viewModel.Groups[0].Title);
    Assert.Equal("Tema: Native Swift", viewModel.Groups[0].Rows[0].Title);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task SearchAsyncClearsGroupsForBlankQuery()
  {
    var handler = new RecordingHandler(CombinedSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client) { Query = "   " };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Groups);
    Assert.Empty(handler.Requests);
  }

  [Fact]
  public async Task SearchAsyncTreatsNullQueryAsBlank()
  {
    var handler = new RecordingHandler(CombinedSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client) { Query = null! };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal(string.Empty, viewModel.Query);
    Assert.Empty(viewModel.Groups);
    Assert.Empty(handler.Requests);
  }

  [Fact]
  public void MapGroupsIgnoresMissingResponseCollections()
  {
    var groups = OmnisearchViewModel.MapGroups(new CombinedSearchResponse(null, null, null, null, null));

    Assert.Empty(groups);
  }

  [Fact]
  public async Task LoadDomainsAsyncBuildsNativeRows()
  {
    var handler = new RecordingHandler(HostnamesJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client) { Query = "example" };

    await viewModel.LoadDomainsAsync(TestContext.Current.CancellationToken);

    Assert.StartsWith("/api/v1/hostnames?", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.Contains("query=example", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.Equal(OmnisearchWebSearchSurface.Domains, viewModel.ActiveSurface);
    var row = Assert.Single(Assert.Single(viewModel.Groups).Rows);
    Assert.Equal("example.com", row.Title);
    Assert.Contains("Trusted", row.Detail, StringComparison.Ordinal);
    Assert.Equal(OmnisearchResultRoute.Domain("hostname-1"), row.Route);
  }

  [Fact]
  public async Task OpenDomainRowLoadsNativeDetailAndActions()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnamesJson),
      new RecordedResponse(HostnameDetailJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: AuthenticatedViewerProvider());

    await viewModel.LoadDomainsAsync(TestContext.Current.CancellationToken);
    await viewModel.OpenRowAsync(viewModel.Groups[0].Rows[0], TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/hostnames/hostname-1", handler.PathAndQuery);
    Assert.True(viewModel.HasSelectedHostname);
    Assert.True(viewModel.CanVoteSelectedHostname);
    Assert.Contains(viewModel.Groups, group => group.Title == "Domain actions");
    Assert.Contains(viewModel.Groups.SelectMany(group => group.Rows), row => row.PrimaryAction == "sentiment.like");
  }

  [Fact]
  public async Task AnonymousDomainDetailHidesAndSkipsAuthenticatedActions()
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    var handler = new RecordingHandler(HostnameDetailJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: viewerProvider);

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasSelectedHostname);
    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.False(viewModel.CanUseSelectedHostnameActions);
    Assert.DoesNotContain(viewModel.Groups, group => group.Title == "Domain actions");
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task SearchAsyncClearsSelectedWebSearchActions()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailJson),
      new RecordedResponse(CombinedSearchJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client) { Query = "native" };

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasSelectedHostname);
    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.False(viewModel.CanUseSelectedHostnameActions);
    Assert.Equal(OmnisearchWebSearchSurface.Search, viewModel.ActiveSurface);
  }

  [Fact]
  public async Task LoadUrlDetailShowsCrawlControlsOnlyWhenAllowed()
  {
    var handler = new RecordingHandler(UrlDetailJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: AdministratorViewerProvider());

    await viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/urls/url-1", handler.PathAndQuery);
    Assert.True(viewModel.HasSelectedUrl);
    Assert.Contains(viewModel.Groups.SelectMany(group => group.Rows), row => row.PrimaryAction == "Trigger crawl");
    Assert.Contains(viewModel.Groups.SelectMany(group => group.Rows), row => row.Route == OmnisearchResultRoute.CrawlRecord("url-1", "crawl-1"));
  }

  [Fact]
  public async Task LoadUrlDetailHidesTriggerWhenApiDisallowsCrawl()
  {
    var handler = new RecordingHandler(UrlDetailCannotTriggerJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client);

    await viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken);
    await viewModel.TriggerSelectedUrlCrawlAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasSelectedUrl);
    Assert.False(viewModel.CanTriggerSelectedUrlCrawl);
    Assert.DoesNotContain(viewModel.Groups.SelectMany(group => group.Rows), row => row.PrimaryAction == "Trigger crawl");
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task OpenCrawlHistoryRowLoadsCrawlList()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(UrlDetailJson),
      new RecordedResponse(UrlCrawlsJson),
      new RecordedResponse(UrlDetailJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: AuthenticatedViewerProvider());

    await viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken);
    var row = viewModel.Groups.SelectMany(group => group.Rows).Single(row => row.Route == OmnisearchResultRoute.CrawlHistory("url-1"));
    await viewModel.OpenRowAsync(row, TestContext.Current.CancellationToken);

    Assert.Equal(3, handler.Requests.Count);
    Assert.Equal("/api/v1/urls/url-1/crawls?limit=25", handler.Requests[1].PathAndQuery);
    Assert.Equal("/api/v1/urls/url-1", handler.Requests[2].PathAndQuery);
    Assert.Equal("Crawl history", viewModel.Groups[0].Title);
    Assert.Equal(OmnisearchResultRoute.CrawlRecord("url-1", "crawl-2"), viewModel.Groups[0].Rows[0].Route);
  }

  [Fact]
  public async Task RouteContextLoadsUrlDetail()
  {
    var routeContextStore = new OmnisearchWebSearchRouteContextStore();
    routeContextStore.Set(new(OmnisearchWebSearchSurface.Urls, "url-1"));
    var handler = new RecordingHandler(UrlDetailJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, routeContextStore);

    await viewModel.LoadRouteContextAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/urls/url-1", handler.PathAndQuery);
    Assert.Equal(OmnisearchWebSearchSurface.Urls, viewModel.ActiveSurface);
    Assert.True(viewModel.HasSelectedUrl);
  }

  [Fact]
  public async Task RouteContextLoadsUrlCrawlHistory()
  {
    var routeContextStore = new OmnisearchWebSearchRouteContextStore();
    routeContextStore.Set(new(
        OmnisearchWebSearchSurface.Urls,
        "url-1",
        DetailKind: OmnisearchWebSearchDetailKind.UrlCrawls));
    var handler = new RecordingHandler([
      new RecordedResponse(UrlCrawlsJson),
      new RecordedResponse(UrlDetailJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, routeContextStore);

    await viewModel.LoadRouteContextAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal("/api/v1/urls/url-1/crawls?limit=25", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/urls/url-1", handler.Requests[1].PathAndQuery);
    Assert.Equal("Crawl history", viewModel.Groups[0].Title);
  }

  [Fact]
  public async Task RouteContextLoadsDomainAndUrlLists()
  {
    var routeContextStore = new OmnisearchWebSearchRouteContextStore();
    routeContextStore.Set(new(OmnisearchWebSearchSurface.Domains));
    var handler = new RecordingHandler([
      new RecordedResponse(HostnamesJson),
      new RecordedResponse(UrlsJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, routeContextStore);

    await viewModel.LoadRouteContextAsync(TestContext.Current.CancellationToken);
    routeContextStore.Set(new(OmnisearchWebSearchSurface.Urls));
    await viewModel.LoadRouteContextAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/hostnames?limit=25", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/urls?limit=25", handler.Requests[1].PathAndQuery);
    Assert.Equal("URLs", viewModel.Groups[0].Title);
  }

  [Fact]
  public async Task SelectedNativeActionsSendExpectedRequests()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailJson),
      new RecordedResponse("{}"),
      new RecordedResponse("{}"),
      new RecordedResponse("{}"),
      new RecordedResponse(UrlDetailJson),
      new RecordedResponse("""{ "success": true, "message": "queued" }"""),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewerProvider = AuthenticatedViewerProvider();
    var viewModel = new OmnisearchViewModel(client, viewerProvider: viewerProvider);

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await viewModel.MuteSelectedHostnameAsync(TestContext.Current.CancellationToken);
    await viewModel.BlockSelectedHostnameAsync(TestContext.Current.CancellationToken);
    viewerProvider.SetViewer(new NavigationViewer(true, ["administrator"]));
    await viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken);
    await viewModel.TriggerSelectedUrlCrawlAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/hostnames/hostname-1/vote", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"choice\":\"like\"", handler.Requests[1].Body, StringComparison.Ordinal);
    Assert.Equal("/api/v1/bookmarks/url_hostname/hostname-1/mute", handler.Requests[2].PathAndQuery);
    Assert.Equal("/api/v1/bookmarks/url_hostname/hostname-1/block", handler.Requests[3].PathAndQuery);
    Assert.Equal("/api/v1/urls/url-1/crawl", handler.Requests[5].PathAndQuery);
  }

  [Fact]
  public async Task NativeLoadErrorSurfacesErrorState()
  {
    var handler = new RecordingHandler("{}", HttpStatusCode.InternalServerError);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client);

    await viewModel.LoadUrlsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Empty(viewModel.Groups);
  }

  [Fact]
  public void ResultRouteParsersRejectInvalidRoutes()
  {
    Assert.False(OmnisearchResultRoute.TryParseDomain(null, out _));
    Assert.False(OmnisearchResultRoute.TryParseWebAddress("domain:hostname-1", out _));
    Assert.True(OmnisearchResultRoute.TryParseCrawlHistory("url-crawls:url-1", out var crawlsUrlId));
    Assert.Equal("url-1", crawlsUrlId);
    Assert.False(OmnisearchResultRoute.TryParseCrawlRecord("crawl:url-1", out _, out _));
    Assert.False(OmnisearchResultRoute.TryParseCrawlRecord("crawl::crawl-1", out _, out _));
    Assert.True(OmnisearchResultRoute.TryParseCrawlRecord("crawl:url-1:crawl-1", out var urlId, out var crawlId));
    Assert.Equal("url-1", urlId);
    Assert.Equal("crawl-1", crawlId);
  }

  private const string HostnamesJson = """
      {
        "results": [{ "__entity_type": "hostname", "entity_id": "hostname-1" }],
        "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
        "hostnames": {
          "hostname-1": {
            "__entity_type": "hostname",
            "id": "hostname-1",
            "hostname": "example.com",
            "is_blocked": false,
            "is_crawlable": true,
            "should_follow_link_rel": true
          }
        },
        "hostname_elections": {
          "hostname-1": {
            "__entity_type": "hostname_election",
            "id": "hostname-1",
            "votes_score_net": 5,
            "votes_count_up": 6,
            "votes_count_down": 1
          }
        }
      }
      """;

  private const string HostnameDetailJson = """
      {
        "hostname": {
          "__entity_type": "hostname",
          "id": "hostname-1",
          "hostname": "example.com",
          "is_blocked": false,
          "is_crawlable": true
        },
        "topic": { "id": "topic-1", "name": "Example", "slug": "example", "topic_type": "company" },
        "top_urls": [{ "id": "url-1", "pathname": "/native", "url": "https://example.com/native" }],
        "rss_feeds": [],
        "hostname_election": {
          "__entity_type": "hostname_election",
          "id": "hostname-1",
          "votes_score_net": 1,
          "votes_count_up": 2,
          "votes_count_down": 1
        },
        "election_vote": null
      }
      """;

  private const string UrlDetailJson = """
      {
        "url": {
          "__entity_type": "url",
          "id": "url-1",
          "url": "https://example.com/native",
          "pathname": "/native",
          "hostname": {
            "__entity_type": "hostname",
            "id": "hostname-1",
            "hostname": "example.com"
          }
        },
        "latest_crawl": {
          "__entity_type": "crawl",
          "id": "crawl-1",
          "url_id": "url-1",
          "created_at": "2026-07-01T00:00:00Z",
          "completed_at": "2026-07-01T00:01:00Z",
          "response_status_code": 200,
          "title": "Native result",
          "markdown": "# Native result"
        },
        "can_view_latest_crawl": true,
        "can_view_crawl_history": true,
        "can_trigger_crawl": true,
        "url_type": "web",
        "rss_feed_id": null
      }
      """;

  private const string UrlDetailCannotTriggerJson = """
      {
        "url": {
          "__entity_type": "url",
          "id": "url-1",
          "url": "https://example.com/native",
          "pathname": "/native",
          "hostname": {
            "__entity_type": "hostname",
            "id": "hostname-1",
            "hostname": "example.com"
          }
        },
        "latest_crawl": null,
        "can_view_latest_crawl": false,
        "can_view_crawl_history": true,
        "can_trigger_crawl": false,
        "url_type": "web",
        "rss_feed_id": null
      }
      """;

  private const string UrlCrawlsJson = """
      {
        "results": [
          {
            "__entity_type": "crawl",
            "id": "crawl-2",
            "url_id": "url-1",
            "created_at": "2026-07-02T00:00:00Z",
            "completed_at": null,
            "response_status_code": null,
            "title": null,
            "markdown": null
          }
        ],
        "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
      }
      """;

  private const string UrlsJson = """
      {
        "results": [
          {
            "__entity_type": "url",
            "id": "url-1",
            "url": "https://example.com/native",
            "pathname": "/native",
            "hostname": { "id": "hostname-1", "hostname": "example.com" }
          }
        ],
        "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
      }
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

}

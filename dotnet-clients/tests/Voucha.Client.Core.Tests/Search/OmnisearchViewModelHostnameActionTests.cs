using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed class OmnisearchViewModelHostnameActionTests
{
  [Fact]
  public async Task HostnameVotesReconcileRawCountsWhilePreservingTheServerOwnedTrustBadge()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailWithMutableElectionJson),
      new RecordedResponse("{}"),
      new RecordedResponse("{}"),
      new RecordedResponse("{}"),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]));
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    Assert.Equal(("Neutral", "4 up, 0 down"), TrustRow(viewModel));
    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Vouch, TestContext.Current.CancellationToken);
    Assert.Equal(("Neutral", "5 up, 0 down"), TrustRow(viewModel));

    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Dislike, TestContext.Current.CancellationToken);
    Assert.Equal(("Neutral", "4 up, 1 down"), TrustRow(viewModel));

    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Neutral, TestContext.Current.CancellationToken);
    Assert.Equal(("Neutral", "4 up, 0 down"), TrustRow(viewModel));
    Assert.False(viewModel.CanClearSelectedHostnameVote);
    Assert.Equal(4, handler.Requests.Count);
    Assert.Equal(System.Net.Http.HttpMethod.Put, handler.Requests[3].Method);
  }

  [Fact]
  public async Task FailedHostnameVoteRestoresRenderedAggregateStateAndReportsError()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailWithMutableElectionJson),
      new RecordedResponse("vote failed", System.Net.HttpStatusCode.InternalServerError),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]));
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(("Neutral", "4 up, 0 down"), TrustRow(viewModel));
    Assert.False(viewModel.CanClearSelectedHostnameVote);
    Assert.Equal("Voucha API request failed with HTTP 500.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task EmailVerificationRequiredHostnameVoteRestoresStateAndRequestsRecovery()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailWithMutableElectionJson),
      new RecordedResponse("{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}", System.Net.HttpStatusCode.Forbidden),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]));
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(("Neutral", "4 up, 0 down"), TrustRow(viewModel));
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task OverlappingHostnameVotesIgnoreSecondMutationUntilFirstFinishes()
  {
    var handler = new DeferredHostnameVoteHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]));
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    var firstVote = viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await handler.VoteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Dislike, TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.Equal(2, handler.Requests.Count);
    handler.FailVote();
    await firstVote;

    Assert.Equal(("Neutral", "4 up, 0 down"), TrustRow(viewModel));
    Assert.False(viewModel.CanClearSelectedHostnameVote);
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task PendingHostnameVoteFailureCannotRestoreAReplacedSurface()
  {
    var handler = new DeferredHostnameVoteHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]));
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    var vote = viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await handler.VoteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken);
    handler.FailVote();
    await vote;

    Assert.Equal(OmnisearchWebSearchSurface.Urls, viewModel.ActiveSurface);
    Assert.Equal("URL detail", Assert.Single(viewModel.Groups).Title);
    Assert.DoesNotContain(viewModel.Groups.SelectMany(group => group.Rows), row => row.Kind == "Trust");
  }

  [Fact]
  public async Task PendingHostnameVoteFailureRefreshesAReselectedHostname()
  {
    var handler = new DeferredHostnameVoteHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]));
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    var vote = viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await handler.VoteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken);
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    Assert.False(viewModel.CanVoteSelectedHostname);

    handler.FailVote();
    await vote;

    Assert.True(viewModel.CanVoteSelectedHostname);
    Assert.Equal(("Neutral", "4 up, 0 down"), TrustRow(viewModel));
  }

  [Fact]
  public async Task OfficialViewerCanModerateHostnameButCannotVote()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailJson),
      new RecordedResponse("{}"),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider(["administrator"]));

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await viewModel.MuteSelectedHostnameAsync(TestContext.Current.CancellationToken);

    var rows = viewModel.Groups.SelectMany(group => group.Rows).ToArray();
    Assert.True(viewModel.HasSelectedHostname);
    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.True(viewModel.CanUseSelectedHostnameActions);
    Assert.DoesNotContain(rows, row => row.PrimaryAction is "sentiment.like" or "sentiment.dislike");
    Assert.Contains(rows, row => row.PrimaryAction == "Mute");
    Assert.Contains(rows, row => row.PrimaryAction == "Block");
    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal("/api/v1/bookmarks/url_hostname/hostname-1/mute", handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task OfficialViewerCanClearTheirHydratedHostnameVoteWithoutCreatingOne()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailWithCurrentVoteJson),
      new RecordedResponse("{}"),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider(["administrator"]));

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.True(viewModel.CanClearSelectedHostnameVote);

    await viewModel.VoteSelectedHostnameAsync(null, TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanClearSelectedHostnameVote);
    Assert.Equal("/api/v1/hostnames/hostname-1/vote", handler.Requests[1].PathAndQuery);
    Assert.Equal(System.Net.Http.HttpMethod.Delete, handler.Requests[1].Method);
  }

  [Fact]
  public async Task AnonymousDomainDetailDoesNotRouteTopUrlsToAuthenticatedUrlDetail()
  {
    var handler = new RecordingHandler(HostnameDetailWithTopUrlJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client);

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    var row = viewModel.Groups.SelectMany(group => group.Rows).Single(row => row.Kind == "URL");
    await viewModel.OpenRowAsync(row, TestContext.Current.CancellationToken);

    Assert.Null(row.Route);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task FailedWebLoadClearsPreviouslySelectedActions()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailJson),
      new RecordedResponse("{}", System.Net.HttpStatusCode.InternalServerError),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]));

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    await viewModel.LoadUrlsAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasSelectedHostname);
    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.False(viewModel.CanUseSelectedHostnameActions);
    Assert.True(viewModel.HasError);
  }

  [Fact]
  public async Task FailedCombinedSearchClearsPreviouslySelectedActions()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(HostnameDetailJson),
      new RecordedResponse("{}", System.Net.HttpStatusCode.InternalServerError),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([])) { Query = "native" };

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasSelectedHostname);
    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.False(viewModel.CanUseSelectedHostnameActions);
    Assert.True(viewModel.HasError);
  }

  [Fact]
  public async Task DomainTrustBadgeUsesDocumentedThresholds()
  {
    var handler = new RecordingHandler(HostnameDetailLowSignalElectionJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]));

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.Groups.SelectMany(group => group.Rows), row => row is { Kind: "Trust", Title: "Neutral" });
  }

  [Theory]
  [InlineData(OmnisearchWebSearchSurface.Urls, OmnisearchWebSearchDetailKind.BookmarkedUrls, "saved", UrlsJson,
      "/api/v1/users/alice/urls/saved?limit=25")]
  [InlineData(OmnisearchWebSearchSurface.Domains, OmnisearchWebSearchDetailKind.BookmarkedHostnames, "muted", HostnameCollectionJson,
      "/api/v1/users/alice/domains/muted?limit=25")]
  public async Task RouteContextLoadsBookmarkedWebSearchCollections(
      OmnisearchWebSearchSurface surface,
      OmnisearchWebSearchDetailKind detailKind,
      string listType,
      string responseJson,
      string expectedPath)
  {
    var routeContextStore = new OmnisearchWebSearchRouteContextStore();
    routeContextStore.Set(new(surface, listType, DetailKind: detailKind));
    var handler = new RecordingHandler(responseJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, routeContextStore, sessionStore: new StubSessionStore());

    await viewModel.LoadRouteContextAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expectedPath, handler.PathAndQuery);
    Assert.True(viewModel.HasGroups);
  }

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
        "top_urls": [],
        "rss_feeds": [],
        "hostname_election": null,
        "election_vote": null
      }
      """;

  private const string HostnameDetailWithTopUrlJson = """
      {
        "hostname": {
          "__entity_type": "hostname",
          "id": "hostname-1",
          "hostname": "example.com",
          "is_blocked": false,
          "is_crawlable": true
        },
        "topic": null,
        "top_urls": [{ "id": "url-1", "pathname": "/native", "url": "https://example.com/native" }],
        "rss_feeds": [],
        "hostname_election": null,
        "election_vote": null
      }
      """;

  private const string HostnameDetailWithCurrentVoteJson = """
      {
        "hostname": {
          "__entity_type": "hostname",
          "id": "hostname-1",
          "hostname": "example.com",
          "is_blocked": false,
          "is_crawlable": true
        },
        "topic": null,
        "top_urls": [],
        "rss_feeds": [],
        "hostname_election": null,
        "election_vote": {
          "__entity_type": "election_vote",
          "entity_id": "hostname-1",
          "user_id": "admin-1",
          "choice": "like",
          "created_at": "2026-01-01T00:00:00Z"
        }
      }
      """;

  private const string HostnameDetailLowSignalElectionJson = """
      {
        "hostname": {
          "__entity_type": "hostname",
          "id": "hostname-1",
          "hostname": "example.com",
          "is_blocked": false,
          "is_crawlable": true
        },
        "topic": null,
        "top_urls": [],
        "rss_feeds": [],
        "hostname_election": {
          "__entity_type": "hostname_election",
          "id": "hostname-1",
          "votes_score_net": 1,
          "votes_count_up": 1,
          "votes_count_down": 0
        },
        "election_vote": null
      }
      """;

  private const string HostnameDetailWithMutableElectionJson = """
      {
        "hostname": {
          "__entity_type": "hostname",
          "id": "hostname-1",
          "hostname": "example.com",
          "is_blocked": false,
          "is_crawlable": true
        },
        "topic": null,
        "top_urls": [],
        "rss_feeds": [],
        "hostname_election": {
          "__entity_type": "hostname_election",
          "id": "hostname-1",
          "votes_score_net": 2,
          "votes_count_up": 4,
          "votes_count_down": 0
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
          "hostname": { "id": "hostname-1", "hostname": "example.com" }
        },
        "latest_crawl": null,
        "can_view_latest_crawl": false,
        "can_view_crawl_history": false,
        "can_trigger_crawl": false,
        "url_type": "web",
        "rss_feed_id": null
      }
      """;

  private const string HostnameCollectionJson = """
      {
        "results": [
          {
            "__entity_type": "hostname",
            "id": "hostname-1",
            "hostname": "example.com",
            "is_blocked": false,
            "is_crawlable": true
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

  private static MutableNavigationViewerProvider ViewerProvider(IReadOnlyList<string> roles)
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(new NavigationViewer(true, roles));
    return viewerProvider;
  }

  private static (string Title, string Detail) TrustRow(OmnisearchViewModel viewModel)
  {
    var row = viewModel.Groups.SelectMany(group => group.Rows).Single(row => row.Kind == "Trust");
    return (row.Title, row.Detail);
  }

  private sealed class StubSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; } =
        new(new User("user-1", "alice", Roles: []));

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class DeferredHostnameVoteHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource<HttpResponseMessage> voteCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource VoteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<RecordedRequest> Requests { get; } = [];

    public void FailVote() => voteCompletion.SetResult(Response("vote failed", System.Net.HttpStatusCode.InternalServerError));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Requests.Add(new(request.Method, request.RequestUri?.PathAndQuery, null));
      return request.RequestUri?.AbsolutePath switch
      {
        "/api/v1/hostnames/hostname-1" => Response(HostnameDetailWithMutableElectionJson),
        "/api/v1/hostnames/hostname-1/vote" => await WaitForVoteAsync(cancellationToken).ConfigureAwait(false),
        "/api/v1/urls/url-1" => Response(UrlDetailJson),
        _ => throw new InvalidOperationException($"Unexpected {request.RequestUri}"),
      };
    }

    private async Task<HttpResponseMessage> WaitForVoteAsync(CancellationToken cancellationToken)
    {
      VoteStarted.TrySetResult();
      return await voteCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HttpResponseMessage Response(string body, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body), };
  }
}

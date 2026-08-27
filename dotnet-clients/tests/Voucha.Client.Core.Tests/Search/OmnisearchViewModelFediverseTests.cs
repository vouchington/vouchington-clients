using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  [Fact]
  public async Task FediverseModeSearchAsyncCallsFediverseSearchAndBuildsGroups()
  {
    var handler = new RecordingHandler(FediverseSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, mode: OmnisearchMode.Fediverse) { Query = "native video" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/fediverse/search?limit=10&q=native%20video", handler.PathAndQuery);
    Assert.False(viewModel.SupportsWebSearchNavigation);
    var group = Assert.Single(viewModel.Groups);
    Assert.Equal("PeerTube", group.Title);
    var row = Assert.Single(group.Rows);
    Assert.Equal("Video", row.Kind);
    Assert.Equal("Native video", row.Title);
    Assert.Equal("Alice on videos.example", row.Detail);
    Assert.True(row.CanOpen);
    Assert.Equal(new Uri("https://videos.example/watch/1"), row.ExternalUrl);
  }

  [Fact]
  public async Task FediverseModeSearchInitialQueryRunsOnce()
  {
    var handler = new RecordingHandler(FediverseSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(
        client,
        initialQuery: " native video ",
        mode: OmnisearchMode.Fediverse);

    await viewModel.SearchInitialQueryAsync(TestContext.Current.CancellationToken);
    var firstRequest = handler.PathAndQuery;
    await viewModel.SearchInitialQueryAsync(TestContext.Current.CancellationToken);

    Assert.Equal("native video", viewModel.Query);
    Assert.Equal("/api/v1/fediverse/search?limit=10&q=native%20video", firstRequest);
    Assert.Equal(firstRequest, handler.PathAndQuery);
  }

  [Fact]
  public async Task FediverseModeSearchPreservesProviderFilter()
  {
    var handler = new RecordingHandler(FediverseSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(
        client,
        initialQuery: "native video",
        fediverseProviders: "peertube",
        mode: OmnisearchMode.Fediverse);

    await viewModel.SearchInitialQueryAsync(TestContext.Current.CancellationToken);

    Assert.Contains("providers=peertube", handler.PathAndQuery, StringComparison.Ordinal);
  }

  [Fact]
  public async Task FediverseModeSearchPreservesLemmyProviderFilter()
  {
    var handler = new RecordingHandler(FediverseSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(
        client,
        initialQuery: "native video",
        fediverseProviders: "lemmy",
        mode: OmnisearchMode.Fediverse);

    await viewModel.SearchInitialQueryAsync(TestContext.Current.CancellationToken);

    Assert.Contains("providers=lemmy", handler.PathAndQuery, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ApplySearchQueryAsyncReplacesExistingFediverseResults()
  {
    var handler = new RecordingHandler(FediverseSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, mode: OmnisearchMode.Fediverse) { Query = "first" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);
    await viewModel.ApplySearchQueryAsync(" second ", TestContext.Current.CancellationToken);

    Assert.Equal("second", viewModel.Query);
    Assert.Equal("/api/v1/fediverse/search?limit=10&q=second", handler.PathAndQuery);
  }

  [Fact]
  public async Task ApplyFediverseSearchQueryAsyncReplacesProviderFilter()
  {
    var handler = new RecordingHandler(FediverseSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(
        client,
        fediverseProviders: "mastodon",
        mode: OmnisearchMode.Fediverse)
    {
      Query = "first",
    };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);
    await viewModel.ApplyFediverseSearchQueryAsync(
        " second ",
        "peertube",
        TestContext.Current.CancellationToken);

    Assert.Equal("second", viewModel.Query);
    Assert.Contains("providers=peertube", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.DoesNotContain("providers=mastodon", handler.PathAndQuery, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(FediverseProvider.All, "/api/v1/fediverse/search?limit=10&q=native")]
  [InlineData(FediverseProvider.PeerTube, "/api/v1/fediverse/search?limit=10&providers=peertube&q=native")]
  [InlineData(FediverseProvider.Mastodon, "/api/v1/fediverse/search?limit=10&providers=mastodon&q=native")]
  [InlineData(FediverseProvider.Lemmy, "/api/v1/fediverse/search?limit=10&providers=lemmy&q=native")]
  [InlineData(FediverseProvider.Bluesky, "/api/v1/fediverse/search?limit=10&providers=bluesky&q=native")]
  public async Task SelectFediverseProviderAsyncRunsOneCanonicalSearch(
      FediverseProvider provider,
      string expectedPathAndQuery)
  {
    var handler = new RecordingHandler(FediverseSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, mode: OmnisearchMode.Fediverse) { Query = "native" };

    await viewModel.SelectFediverseProviderAsync(provider, TestContext.Current.CancellationToken);

    Assert.Equal(provider, viewModel.SelectedFediverseProvider);
    Assert.Equal(expectedPathAndQuery, handler.PathAndQuery);
  }

  [Theory]
  [InlineData(false, FediverseProvider.Mastodon, FediverseProvider.PeerTube, false)]
  [InlineData(true, FediverseProvider.Mastodon, FediverseProvider.Mastodon, false)]
  [InlineData(true, FediverseProvider.Mastodon, FediverseProvider.PeerTube, true)]
  public void ProviderSelectionRoutesOnlyCheckedUserChanges(
      bool isChecked,
      FediverseProvider selected,
      FediverseProvider requested,
      bool expected) =>
      Assert.Equal(expected, FediverseProviderSelection.ShouldRoute(isChecked, selected, requested));

  [Theory]
  [InlineData(FediverseProvider.All, " native ", "/fediverse?q=native")]
  [InlineData(FediverseProvider.PeerTube, "native search", "/fediverse?q=native%20search&provider=peertube")]
  [InlineData(FediverseProvider.Mastodon, "native search", "/fediverse?q=native%20search&provider=mastodon")]
  [InlineData(FediverseProvider.Lemmy, "native search", "/fediverse?q=native%20search&provider=lemmy")]
  [InlineData(FediverseProvider.Bluesky, null, "/fediverse?provider=bluesky")]
  public void FediverseProviderRouteIsCanonical(
      FediverseProvider provider,
      string? query,
      string expected) =>
      Assert.Equal(expected, FediverseSearchRoute.Build(provider, query));

  [Theory]
  [InlineData("voucha://fediverse?q=native%20search", FediverseProvider.All, "/api/v1/fediverse/search?limit=10&q=native%20search")]
  [InlineData("voucha://fediverse?q=native%20search&provider=peertube", FediverseProvider.PeerTube, "/api/v1/fediverse/search?limit=10&providers=peertube&q=native%20search")]
  [InlineData("voucha://fediverse?q=native%20search&provider=mastodon", FediverseProvider.Mastodon, "/api/v1/fediverse/search?limit=10&providers=mastodon&q=native%20search")]
  [InlineData("voucha://fediverse?q=native%20search&provider=lemmy", FediverseProvider.Lemmy, "/api/v1/fediverse/search?limit=10&providers=lemmy&q=native%20search")]
  [InlineData("voucha://fediverse?q=native%20search&provider=bluesky", FediverseProvider.Bluesky, "/api/v1/fediverse/search?limit=10&providers=bluesky&q=native%20search")]
  [InlineData("voucha://fediverse?q=native%20search&provider=invalid", FediverseProvider.All, "/api/v1/fediverse/search?limit=10&q=native%20search")]
  public async Task FediverseDeepLinkRestoresCanonicalProviderOrFallsBackToAll(
      string deepLink,
      FediverseProvider expectedProvider,
      string expectedPathAndQuery)
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        deepLink,
        new NavigationViewer(true, [], new Dictionary<string, bool> { ["fediverse"] = true }));
    var match = Assert.IsType<NativeRouteMatch>(resolution.Match);
    var handler = new RecordingHandler(FediverseSearchJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, mode: OmnisearchMode.Fediverse);

    await viewModel.ApplyFediverseSearchQueryAsync(
        match.QueryValue("q", "query"),
        match.QueryValue("provider"),
        TestContext.Current.CancellationToken);

    Assert.Equal("native search", viewModel.Query);
    Assert.Equal(expectedProvider, viewModel.SelectedFediverseProvider);
    Assert.Equal(expectedPathAndQuery, handler.PathAndQuery);
  }

  [Fact]
  public async Task ProviderChangeCancelsPriorRequestAndIgnoresItsStaleResponse()
  {
    var handler = new DeferredFediverseSearchHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, mode: OmnisearchMode.Fediverse) { Query = "native" };

    var initialSearch = viewModel.SearchAsync(TestContext.Current.CancellationToken);
    await handler.WaitForRequestCountAsync(1, TestContext.Current.CancellationToken);

    var providerSearch = viewModel.SelectFediverseProviderAsync(
        FediverseProvider.Mastodon,
        TestContext.Current.CancellationToken);
    await handler.WaitForCancellationAsync(0, TestContext.Current.CancellationToken);
    await handler.WaitForRequestCountAsync(2, TestContext.Current.CancellationToken);

    handler.CompleteRequest(1, SearchResponse("mastodon", "Current result"));
    await providerSearch;
    handler.CompleteRequest(0, SearchResponse("peertube", "Stale result"));
    await initialSearch;

    Assert.Equal(FediverseProvider.Mastodon, viewModel.SelectedFediverseProvider);
    Assert.Equal("/api/v1/fediverse/search?limit=10&q=native", handler.PathAndQuery(0));
    Assert.Equal(
        "/api/v1/fediverse/search?limit=10&providers=mastodon&q=native",
        handler.PathAndQuery(1));
    var row = Assert.Single(Assert.Single(viewModel.Groups).Rows);
    Assert.Equal("Current result", row.Title);
    Assert.False(viewModel.IsLoading);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public void MapFediverseGroupsFiltersUnsafeExternalUrls()
  {
    var groups = OmnisearchViewModel.MapFediverseGroups(new FediverseSearchResponse([
      new FediverseSearchBucket("mastodon", "ok", [
        new FediverseSearchResult(
            "mastodon",
            "post",
            new Uri("data:text/html,hello"),
            "Unsafe post",
            "",
            SourceHostname: "social.example"),
      ]),
    ]));

    Assert.Null(Assert.Single(Assert.Single(groups).Rows).ExternalUrl);
  }

  [Fact]
  public void MapFediverseGroupsLabelsProfileResultsAsProfile()
  {
    var groups = OmnisearchViewModel.MapFediverseGroups(new FediverseSearchResponse([
      new FediverseSearchBucket("mastodon", "ok", [
        new FediverseSearchResult(
            "mastodon",
            "profile",
            new Uri("https://social.example/@alice"),
            "Alice",
            "",
            SourceHostname: "social.example"),
      ]),
    ]));

    Assert.Equal("Profile", Assert.Single(Assert.Single(groups).Rows).Kind);
  }

  [Fact]
  public void MapFediverseGroupsKeepsEmptyErrorBuckets()
  {
    var groups = OmnisearchViewModel.MapFediverseGroups(new FediverseSearchResponse([
      new FediverseSearchBucket("peertube", "error", [], ErrorCode: "provider_timeout"),
    ]));

    var row = Assert.Single(Assert.Single(groups).Rows);
    Assert.Equal("Status", row.Kind);
    Assert.Equal("PeerTube unavailable", row.Title);
    Assert.Equal("provider_timeout", row.Detail);
    Assert.False(row.CanOpen);
  }

  [Fact]
  public void MapFediverseGroupsKeepsWarningAfterPartialResults()
  {
    var item = new FediverseSearchResult(
        "mastodon",
        "post",
        new Uri("https://social.example/@alice/1"),
        "Available post",
        "",
        SourceHostname: "social.example");
    var groups = OmnisearchViewModel.MapFediverseGroups(new FediverseSearchResponse([
      new FediverseSearchBucket("mastodon", "partial", [item], ErrorCode: "provider_timeout"),
    ]));

    var rows = Assert.Single(groups).Rows;
    Assert.Equal(2, rows.Count);
    Assert.True(rows[0].CanOpen);
    Assert.Equal("Some results may be unavailable.", rows[1].Title);
    Assert.Equal("provider_timeout", rows[1].Detail);
  }

  [Fact]
  public void MapFediverseGroupsIgnoresEmptyBuckets()
  {
    var groups = OmnisearchViewModel.MapFediverseGroups(new FediverseSearchResponse([
      new FediverseSearchBucket("mastodon", "ok", []),
    ]));

    Assert.Empty(groups);
  }

  private const string FediverseSearchJson = """
      {
        "buckets": [
          {
            "provider": "peertube",
            "status": "ok",
            "items": [
              {
                "provider": "peertube",
                "result_type": "video",
                "external_url": "https://videos.example/watch/1",
                "title": "Native video",
                "summary": "A useful video.",
                "author_name": "Alice",
                "source_hostname": "videos.example"
              }
            ]
          }
        ]
      }
      """;

  private static string SearchResponse(string provider, string title) => $$"""
      {
        "buckets": [{
          "provider": "{{provider}}",
          "status": "ok",
          "items": [{
            "provider": "{{provider}}",
            "result_type": "post",
            "external_url": "https://social.example/post/1",
            "title": "{{title}}",
            "summary": "",
            "source_hostname": "social.example"
          }]
        }]
      }
      """;

  private sealed class DeferredFediverseSearchHandler : HttpMessageHandler
  {
    private readonly Lock gate = new();
    private readonly List<PendingRequest> requests = [];
    private readonly SemaphoreSlim requestsChanged = new(0);

    public void CompleteRequest(int index, string body) =>
        Request(index).Completion.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    public string? PathAndQuery(int index) => Request(index).PathAndQuery;

    public Task WaitForCancellationAsync(int index, CancellationToken cancellationToken) =>
        Request(index).CancellationObserved.Task.WaitAsync(cancellationToken);

    public async Task WaitForRequestCountAsync(int count, CancellationToken cancellationToken)
    {
      while (RequestCount < count)
      {
        await requestsChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
      }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var pending = new PendingRequest(request.RequestUri?.PathAndQuery);
      using var registration = cancellationToken.Register(pending.CancellationObserved.SetResult);
      lock (gate)
      {
        requests.Add(pending);
      }
      requestsChanged.Release();
      var response = await pending.Completion.Task.ConfigureAwait(false);
      response.RequestMessage = request;
      return response;
    }

    private int RequestCount
    {
      get
      {
        lock (gate) return requests.Count;
      }
    }

    private PendingRequest Request(int index)
    {
      lock (gate) return requests[index];
    }

    private sealed record PendingRequest(string? PathAndQuery)
    {
      public TaskCompletionSource CancellationObserved { get; } =
          new(TaskCreationOptions.RunContinuationsAsynchronously);
      public TaskCompletionSource<HttpResponseMessage> Completion { get; } =
          new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
  }
}

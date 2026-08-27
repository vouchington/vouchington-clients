using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicDetailViewModelSourceCrawlsTests
{
  [Fact]
  public async Task LoadSourceCrawlsAsyncAppendsTheNextFeedScopedPageUsingItsOpaqueCursor()
  {
    var handler = new SourceCrawlsHandler();
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(),
        apiClient: new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        canViewSourceCrawlHistory: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlsAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreSourceCrawlsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(
        ["/api/v1/rss-feeds/feed-1/crawls?limit=25", "/api/v1/rss-feeds/feed-1/crawls?after=cursor-1&limit=25"],
        handler.RequestPathsAndQueries);
    Assert.Equal(["crawl-1", "crawl-2"], viewModel.SourceCrawls.Select(crawl => crawl.Id));
    Assert.True(viewModel.HasSourceCrawls);
    Assert.False(viewModel.HasMoreSourceCrawls);
  }

  [Fact]
  public async Task LoadSourceCrawlsAsyncTracksACompletedEmptyInitialPage()
  {
    var handler = new SourceCrawlsHandler { ReturnEmptyInitialList = true };
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(),
        apiClient: new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        canViewSourceCrawlHistory: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    Assert.False(viewModel.HasLoadedSourceCrawlHistory);

    await viewModel.LoadSourceCrawlsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasLoadedSourceCrawlHistory);
    Assert.True(viewModel.ShowsSourceCrawlHistoryEmpty);
    Assert.Empty(viewModel.SourceCrawls);
    Assert.False(viewModel.HasSourceCrawls);
  }

  [Fact]
  public async Task LoadSourceCrawlAsyncUsesTheRequestedFeedScopedDetailEndpoint()
  {
    var handler = new SourceCrawlsHandler();
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(),
        apiClient: new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        canViewSourceCrawlHistory: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlAsync("crawl-2", TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/rss-feeds/feed-1/crawls/crawl-2", Assert.Single(handler.RequestPathsAndQueries));
    Assert.Equal("crawl-2", viewModel.SelectedSourceCrawl?.Id);
    Assert.True(viewModel.HasSelectedSourceCrawl);
  }

  [Fact]
  public async Task LoadMoreSourceCrawlsAsyncRetainsRowsAndCursorAndExposesRetryAfterAFailedContinuation()
  {
    var handler = new SourceCrawlsHandler { FailNextContinuation = true };
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(),
        apiClient: new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        canViewSourceCrawlHistory: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlsAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreSourceCrawlsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["crawl-1"], viewModel.SourceCrawls.Select(crawl => crawl.Id));
    Assert.True(viewModel.HasMoreSourceCrawls);
    Assert.True(viewModel.HasSourceCrawlsContinuationError);
    Assert.NotNull(viewModel.SourceCrawlsContinuationErrorMessage);

    await viewModel.LoadMoreSourceCrawlsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["crawl-1", "crawl-2"], viewModel.SourceCrawls.Select(crawl => crawl.Id));
    Assert.False(viewModel.HasSourceCrawlsContinuationError);
  }

  [Fact]
  public async Task LoadMoreSourceCrawlsAsyncUsesOneInFlightRequestAndDoesNotAppendDuplicateRows()
  {
    var handler = new SourceCrawlsHandler { BlockNextContinuation = true };
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(),
        apiClient: new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        canViewSourceCrawlHistory: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlsAsync(TestContext.Current.CancellationToken);
    var first = viewModel.LoadMoreSourceCrawlsAsync(TestContext.Current.CancellationToken);
    await handler.ContinuationRequested.Task.WaitAsync(TestContext.Current.CancellationToken);
    var second = viewModel.LoadMoreSourceCrawlsAsync(TestContext.Current.CancellationToken);

    handler.ReleaseContinuation();
    await Task.WhenAll(first, second);

    Assert.Equal(2, handler.RequestPathsAndQueries.Count);
    Assert.Equal(["crawl-1", "crawl-2"], viewModel.SourceCrawls.Select(crawl => crawl.Id));
  }

  [Fact]
  public async Task LoadSourceCrawlAsyncClearsTheListCursorAndContinuationStateBeforeLoadingDetail()
  {
    var handler = new SourceCrawlsHandler { FailNextContinuation = true };
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(),
        apiClient: new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        canViewSourceCrawlHistory: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlsAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreSourceCrawlsAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlAsync("crawl-2", TestContext.Current.CancellationToken);
    await viewModel.LoadMoreSourceCrawlsAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.SourceCrawls);
    Assert.False(viewModel.HasMoreSourceCrawls);
    Assert.False(viewModel.HasSourceCrawlsContinuationError);
    Assert.Equal("crawl-2", viewModel.SelectedSourceCrawl?.Id);
    Assert.Equal(3, handler.RequestPathsAndQueries.Count);
  }

  [Fact]
  public async Task LoadSourceCrawlsAsyncDoesNotCallThePaidEndpointWithoutAccess()
  {
    var handler = new SourceCrawlsHandler();
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(),
        apiClient: new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlsAsync(TestContext.Current.CancellationToken);

    Assert.Empty(handler.RequestPathsAndQueries);
    Assert.True(viewModel.HasSourceCrawlHistoryAccessError);
    Assert.Equal("Crawl history is available with Plus or Pro.", viewModel.SourceCrawlHistoryAccessErrorMessage);
  }

  [Fact]
  public async Task SourceCrawlDetailKeepsAdministratorAccessWithoutMembershipRequest()
  {
    var handler = new SourceCrawlsHandler();
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(), apiClient: Client(handler), canManageSourceCrawls: true,
        requiresAuthoritativeSourceCrawlMembership: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    var detail = viewModel.CreateSourceCrawlDetailViewModel();
    await detail.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await detail.LoadSourceCrawlAsync("crawl-2", TestContext.Current.CancellationToken);

    Assert.Equal(["/api/v1/rss-feeds/feed-1/crawls/crawl-2"], handler.RequestPathsAndQueries);
    Assert.Equal("crawl-2", detail.SelectedSourceCrawl?.Id);
  }

  [Fact]
  public async Task SourceCrawlDetailDeniesExpiredPaidMembershipBeforeDetailRequest()
  {
    var handler = new SourceCrawlsHandler { MembershipActive = false };
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(), apiClient: Client(handler), canViewSourceCrawlHistory: true,
        requiresAuthoritativeSourceCrawlMembership: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    var detail = viewModel.CreateSourceCrawlDetailViewModel();
    await detail.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await detail.LoadSourceCrawlAsync("crawl-2", TestContext.Current.CancellationToken);

    Assert.Equal(["/api/v1/memberships/me"], handler.RequestPathsAndQueries);
    Assert.True(detail.HasSourceCrawlHistoryAccessError);
    Assert.Null(detail.SelectedSourceCrawl);
  }

  [Fact]
  public async Task RevokedMembershipClearsLoadedCrawlsBeforeDenyingContinuation()
  {
    var handler = new SourceCrawlsHandler();
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(), apiClient: Client(handler), canViewSourceCrawlHistory: true,
        requiresAuthoritativeSourceCrawlMembership: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlsAsync(TestContext.Current.CancellationToken);
    handler.MembershipActive = false;
    await viewModel.LoadMoreSourceCrawlsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["/api/v1/memberships/me", "/api/v1/rss-feeds/feed-1/crawls?limit=25", "/api/v1/memberships/me"], handler.RequestPathsAndQueries);
    Assert.Empty(viewModel.SourceCrawls);
    Assert.Null(viewModel.SelectedSourceCrawl);
    Assert.False(viewModel.HasLoadedSourceCrawlHistory);
    Assert.False(viewModel.HasMoreSourceCrawls);
    Assert.False(viewModel.HasSourceCrawlsContinuationError);
    Assert.False(viewModel.IsLoadingMoreSourceCrawls);
    Assert.True(viewModel.HasSourceCrawlHistoryAccessError);
  }

  [Fact]
  public async Task InitialCrawlRequestsExposeLocalizedRetryStateAndDoNotShareListStateWithDetailViewModels()
  {
    var handler = new SourceCrawlsHandler { FailNextInitialList = true };
    var viewModel = new TopicDetailViewModel(
        new SourceTopicService(),
        apiClient: new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        canViewSourceCrawlHistory: true);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.LoadSourceCrawlsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasSourceCrawlHistoryRequestError);
    Assert.Equal("Action failed", viewModel.SourceCrawlHistoryRequestErrorMessage);

    await viewModel.RetrySourceCrawlHistoryAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["crawl-1"], viewModel.SourceCrawls.Select(crawl => crawl.Id));

    var detailViewModel = viewModel.CreateSourceCrawlDetailViewModel();
    await detailViewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    handler.FailNextDetail = true;
    await detailViewModel.LoadSourceCrawlAsync("crawl-2", TestContext.Current.CancellationToken);

    Assert.True(detailViewModel.HasSourceCrawlHistoryRequestError);
    Assert.Empty(detailViewModel.SourceCrawls);
    Assert.Equal(["crawl-1"], viewModel.SourceCrawls.Select(crawl => crawl.Id));

    await detailViewModel.RetrySourceCrawlHistoryAsync(TestContext.Current.CancellationToken);
    Assert.Equal("crawl-2", detailViewModel.SelectedSourceCrawl?.Id);
    Assert.False(detailViewModel.HasSourceCrawlHistoryRequestError);
  }

  private sealed class SourceTopicService : ITopicsService
  {
    public Task<TopicResponse> FetchTopicAsync(string topicIdOrSlug, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TopicResponse(new Topic("topic-1", "Source Topic", "source-topic", "rss_feed")));

    public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
        string topicId,
        RssFeedEnabledFilter? enabled = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new RssFeedsResponse(
            [new RssFeedSource("feed-1", "Source", NewsFeedSourceType.Article, null, null, null)],
            new PageInfo(null, false, null)));

    public Task<TopicSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicMutationResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicMutationResponse> UpdateTopicAsync(string topicIdOrSlug, UpdateTopicBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task FollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UnfollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UpdateSourceAsync(string rssFeedId, UpdateRssFeedBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ListResponse<string>> FetchTopicAliasesAsync(string topicId, string? after = null, int? limit = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ListResponse<TopicAdditionalHostname>> FetchTopicAdditionalHostnamesAsync(string topicId, string? after = null, int? limit = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CreateTopicAliasesAsync(string topicId, CreateTopicAliasesBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteTopicAliasAsync(string topicId, string aliasValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicAdditionalHostnameResponse> CreateTopicAdditionalHostnameAsync(string topicId, string hostname, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteTopicAdditionalHostnameAsync(string topicId, string hostnameId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicMergeResponse> MergeTopicAliasesAsync(string sourceTopicId, string destinationIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private static VouchaApiClient Client(SourceCrawlsHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private sealed class SourceCrawlsHandler : HttpMessageHandler
  {
    public List<string?> RequestPathsAndQueries { get; } = [];

    public bool FailNextContinuation { get; set; }
    public bool FailNextInitialList { get; set; }
    public bool FailNextDetail { get; set; }
    public bool ReturnEmptyInitialList { get; set; }
    public bool BlockNextContinuation { get; set; }
    public bool MembershipActive { get; set; } = true;
    public TaskCompletionSource<bool> ContinuationRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<HttpResponseMessage>? blockedContinuation;

    public void ReleaseContinuation() => blockedContinuation?.SetResult(ContinuationResponse());

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      RequestPathsAndQueries.Add(request.RequestUri?.PathAndQuery);
      if (request.RequestUri?.AbsolutePath == "/api/v1/memberships/me")
      {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(MembershipActive ? ActiveMembershipResponse : "{ \"membership\": null }", Encoding.UTF8, "application/json"),
        });
      }
      if (request.RequestUri?.AbsolutePath.EndsWith("/crawls/crawl-2", StringComparison.Ordinal) == true)
      {
        if (FailNextDetail)
        {
          FailNextDetail = false;
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("""
              { "crawl": { "id": "crawl-2", "response_code": 201, "created_at": "2026-08-17T00:01:00Z" } }
              """, Encoding.UTF8, "application/json"),
        });
      }
      var secondPage = request.RequestUri?.Query.Contains("after=cursor-1", StringComparison.Ordinal) == true;
      if (!secondPage && FailNextInitialList)
      {
        FailNextInitialList = false;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
      }
      if (!secondPage && ReturnEmptyInitialList)
      {
        ReturnEmptyInitialList = false;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(
              """{ "results": [], "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null } }""",
              Encoding.UTF8,
              "application/json"),
        });
      }
      if (secondPage && FailNextContinuation)
      {
        FailNextContinuation = false;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
      }
      if (secondPage && BlockNextContinuation)
      {
        BlockNextContinuation = false;
        blockedContinuation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ContinuationRequested.SetResult(true);
        return blockedContinuation.Task;
      }
      return Task.FromResult(ContinuationResponse(secondPage));
    }

    private HttpResponseMessage ContinuationResponse(bool secondPage = true) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(secondPage ? """
            { "results": [
              { "id": "crawl-1", "response_code": 200, "created_at": "2026-08-17T00:00:00Z" },
              { "id": "crawl-2", "response_code": 201, "created_at": "2026-08-17T00:01:00Z" }],
              "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": "cursor-1" } }
            """ : """
            { "results": [{ "id": "crawl-1", "response_code": 200, "created_at": "2026-08-17T00:00:00Z" }],
              "page_info": { "end_cursor": "cursor-1", "has_next_page": true, "start_cursor": null } }
            """, Encoding.UTF8, "application/json"),
    };

    private const string ActiveMembershipResponse = """
        { "membership": { "id": "membership-1", "user_id": "user-1", "plan": "plus", "status": "active", "started_at": "2026-01-01T00:00:00Z", "expires_at": null, "granted_by_id": null, "cancelled_at": null, "expired_at": null, "past_due_at": null, "paused_at": null, "cancel_at_period_end": false, "latest_change_id": null, "created_at": "2026-01-01T00:00:00Z", "updated_at": "2026-01-01T00:00:00Z", "sku": { "id": "sku-1", "plan": "plus", "price": { "amount": 500, "currency": "usd" }, "interval": "monthly", "stripe_price_id": "price-1", "retired_at": null }, "has_stripe_subscription": true } }
        """;
  }
}

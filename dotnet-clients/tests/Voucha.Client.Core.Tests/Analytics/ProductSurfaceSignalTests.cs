using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Analytics;

public sealed class ProductSurfaceSignalTests
{
  [Fact]
  public async Task RssFeedTopicRecordsOneViewAndAReloadDoesNotRepeatIt()
  {
    var signals = new RecordingSignals();
    var viewModel = new TopicDetailViewModel(
        new TopicService("rss_feed", new Dictionary<string, string> { ["rewards"] = "feed-1" }),
        productSignals: signals);

    await viewModel.LoadAsync("rewards", TestContext.Current.CancellationToken);
    await viewModel.LoadAsync("rewards", TestContext.Current.CancellationToken);

    Assert.Equal(["feed-1"], signals.FeedViews);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task ADifferentRssFeedOnTheSamePageRecordsItsOwnView()
  {
    var signals = new RecordingSignals();
    var viewModel = new TopicDetailViewModel(
        new TopicService("rss_feed", new Dictionary<string, string> { ["rewards"] = "feed-1", ["other"] = "feed-2" }),
        productSignals: signals);

    await viewModel.LoadAsync("rewards", TestContext.Current.CancellationToken);
    await viewModel.LoadAsync("other", TestContext.Current.CancellationToken);

    Assert.Equal(["feed-1", "feed-2"], signals.FeedViews);
  }

  [Fact]
  public async Task ReturningToAnEarlierRssFeedRecordsItOnlyOnce()
  {
    var signals = new RecordingSignals();
    var viewModel = new TopicDetailViewModel(
        new TopicService("rss_feed", new Dictionary<string, string> { ["rewards"] = "feed-1", ["other"] = "feed-2" }),
        productSignals: signals);

    await viewModel.LoadAsync("rewards", TestContext.Current.CancellationToken);
    await viewModel.LoadAsync("other", TestContext.Current.CancellationToken);
    await viewModel.LoadAsync("rewards", TestContext.Current.CancellationToken);

    Assert.Equal(["feed-1", "feed-2"], signals.FeedViews);
  }

  [Fact]
  public async Task NonRssTopicAndAFediverseLoadDoNotRecordAFeedView()
  {
    var signals = new RecordingSignals();
    var topic = new TopicDetailViewModel(new TopicService("topic", []), productSignals: signals);
    var fediverse = new TopicDetailViewModel(
        new TopicService("rss_feed", new Dictionary<string, string> { ["rewards"] = "feed-1" }),
        productSignals: signals);

    await topic.LoadAsync("cities", TestContext.Current.CancellationToken);
    await fediverse.LoadAsync("rewards", followSource: false, TestContext.Current.CancellationToken);

    Assert.Empty(signals.FeedViews);
    Assert.Equal(LoadState.Loaded, topic.State);
    Assert.Equal(LoadState.Loaded, fediverse.State);
  }

  [Fact]
  public async Task RssTopicWithoutAFeedDoesNotRecordAView()
  {
    var signals = new RecordingSignals();
    var viewModel = new TopicDetailViewModel(new TopicService("rss_feed", []), productSignals: signals);

    await viewModel.LoadAsync("rewards", TestContext.Current.CancellationToken);

    Assert.Empty(signals.FeedViews);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task RssItemRecordsOneViewWhenTheFetchFailsAndARetryDoesNotRepeatIt()
  {
    var signals = new RecordingSignals();
    var items = new FailingItems();
    var viewModel = new RssFeedItemDetailViewModel(items, "item-1", NewsFeedItemKind.Article, productSignals: signals);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["item-1"], signals.ItemViews);
    Assert.Equal(2, items.Calls);
    Assert.True(viewModel.HasError);
  }

  private sealed class RecordingSignals : IProductSurfaceAnalytics
  {
    public List<string> FeedViews { get; } = [];

    public List<string> ItemViews { get; } = [];

    public void RecordLandingPageVisit(string landingPageId, LandingPageVisitSignal signal) =>
        throw new NotSupportedException();

    public void RecordLandingPageClick(string landingPageId, LandingPageClickSignal signal) =>
        throw new NotSupportedException();

    public void RecordRssFeedView(string rssFeedId) => FeedViews.Add(rssFeedId);

    public void RecordRssFeedItemView(string itemId) => ItemViews.Add(itemId);
  }

  private sealed class FailingItems : IRssFeedItemDetailService
  {
    public int Calls { get; private set; }

    public Task<RssFeedItemDetail> FetchAsync(
        string itemId,
        NewsFeedItemKind kind,
        CancellationToken cancellationToken = default)
    {
      Calls++;
      throw new InvalidOperationException("unavailable");
    }
  }

  private sealed class TopicService(string topicType, Dictionary<string, string> feeds) : ITopicsService
  {
    public Task<TopicResponse> FetchTopicAsync(string topicIdOrSlug, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TopicResponse(new Topic(topicIdOrSlug, topicIdOrSlug, topicIdOrSlug, topicType)));

    public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
        string topicId,
        RssFeedEnabledFilter? enabled = null,
        CancellationToken cancellationToken = default)
    {
      IReadOnlyList<RssFeedSource> results = feeds.TryGetValue(topicId, out var feedId)
          ? [Feed(feedId)]
          : [];
      return Task.FromResult(new RssFeedsResponse(results, new PageInfo(null, false, null)));
    }

    private static RssFeedSource Feed(string feedId) =>
        new(feedId, feedId, NewsFeedSourceType.Article, new RssFeedUrl(new Uri("https://example.com/rss")), null, new RssFeedHostname("example.com"));

    public Task<TopicSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMutationResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMutationResponse> UpdateTopicAsync(string topicIdOrSlug, UpdateTopicBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task FollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UnfollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UpdateSourceAsync(string rssFeedId, UpdateRssFeedBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ListResponse<TopicAlias>> FetchTopicAliasesAsync(string topicId, string? after = null, int? limit = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ListResponse<TopicAdditionalHostname>> FetchTopicAdditionalHostnamesAsync(string topicId, string? after = null, int? limit = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task CreateTopicAliasesAsync(string topicId, CreateTopicAliasesBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteTopicAliasAsync(string topicId, string aliasId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicAdditionalHostnameResponse> CreateTopicAdditionalHostnameAsync(string topicId, string hostname, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteTopicAdditionalHostnameAsync(string topicId, string hostnameId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMergeResponse> MergeTopicAliasesAsync(string sourceTopicId, string destinationIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}

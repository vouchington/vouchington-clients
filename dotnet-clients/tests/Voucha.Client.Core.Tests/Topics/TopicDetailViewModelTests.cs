using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicDetailViewModelTests
{
  [Fact]
  public async Task LoadAsyncLoadsTopicAndFollowStates()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed", "Description"),
          TopicElection: new TopicElection("election-1", "topic-1", 2.5, 3, 1),
          ElectionVote: new ElectionVote("topic_election", "topic-1", "user-1", ElectionVoteChoice.Like, DateTimeOffset.UnixEpoch),
          Bookmarks: new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["topic-1"] = new(Follow: true),
          }),
      RssFeedsResponse = new RssFeedsResponse(
          [
            new RssFeedSource(
                "feed-1",
                "Rewards Feed",
                NewsFeedSourceType.Article,
                new RssFeedUrl(new Uri("https://example.com/feed.xml")),
                new RssFeedUrl(new Uri("https://example.com")),
                new RssFeedHostname("example.com"),
                new Topic("topic-1", "Rewards", "rewards", "rss_feed")),
          ],
          new PageInfo(null, false, null),
          new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["feed-1"] = new(Follow: true),
          },
          null,
          null),
    };
    var viewModel = new TopicDetailViewModel(service, service);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);

    Assert.Equal("topic-1", service.LastTopicId);
    Assert.Equal("Rewards", viewModel.Topic?.Name);
    Assert.True(viewModel.IsFollowingTopic);
    Assert.True(viewModel.IsFollowingSource);
    Assert.True(viewModel.CanFollowSource);
  }
  [Fact]
  public async Task ToggleFollowAsyncUsesTopicBookmarkEndpoints()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(new Topic("topic-1", "Rewards", "rewards", "topic")),
    };
    var viewModel = new TopicDetailViewModel(service, service);

    await viewModel.LoadAsync("topic-1", followSource: false, TestContext.Current.CancellationToken);
    await viewModel.ToggleTopicFollowAsync(TestContext.Current.CancellationToken);

    Assert.Equal(("topic-1", true), service.TopicFollowCalls[0]);
    Assert.True(viewModel.IsFollowingTopic);

    await viewModel.ToggleTopicFollowAsync(TestContext.Current.CancellationToken);

    Assert.Equal(("topic-1", false), service.TopicFollowCalls[1]);
    Assert.False(viewModel.IsFollowingTopic);
  }
  [Fact]
  public async Task ToggleSourceFollowAsyncUsesRssFeedBookmarkEndpoints()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed"),
          null,
          new TopicElection("election-1", "topic-1", 2.5, 3, 1),
          new ElectionVote("topic_election", "topic-1", "user-1", ElectionVoteChoice.Like, DateTimeOffset.UnixEpoch)),
      RssFeedsResponse = new RssFeedsResponse(
          [
            new RssFeedSource(
                "feed-1",
                "Rewards Feed",
                NewsFeedSourceType.Article,
                new RssFeedUrl(new Uri("https://example.com/feed.xml")),
                null,
                new RssFeedHostname("example.com"),
                new Topic("topic-1", "Rewards", "rewards", "rss_feed")),
          ],
          new PageInfo(null, false, null),
          null,
          null,
          null),
    };
    var viewModel = new TopicDetailViewModel(service, service);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.ToggleSourceFollowAsync(TestContext.Current.CancellationToken);

    Assert.Equal(("feed-1", true), service.SourceFollowCalls[0]);
    Assert.True(viewModel.IsFollowingSource);
  }
  [Fact]
  public async Task ToggleMuteAsyncUsesBookmarkEndpoints()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed"),
          null,
          null,
          null,
          new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["topic-1"] = new(Follow: true),
          }),
      RssFeedsResponse = new RssFeedsResponse(
          [
            new RssFeedSource(
                "feed-1",
                "Rewards Feed",
                NewsFeedSourceType.Article,
                new RssFeedUrl(new Uri("https://example.com/feed.xml")),
                null,
                new RssFeedHostname("example.com"),
                new Topic("topic-1", "Rewards", "rewards", "rss_feed")),
          ],
          new PageInfo(null, false, null),
          new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["feed-1"] = new(Follow: true),
            ["topic-1"] = new(Mute: true),
          },
          null,
          null),
    };
    var viewModel = new TopicDetailViewModel(service, service);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.ToggleTopicMuteAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleSourceMuteAsync(TestContext.Current.CancellationToken);

    Assert.Equal(("topic-1", true), service.BookmarkCalls[0]);
    Assert.Equal(("feed-1", true), service.BookmarkCalls[1]);
    Assert.True(viewModel.IsMutedTopic);
    Assert.True(viewModel.IsMutedSource);
    Assert.False(viewModel.IsFollowingTopic);
    Assert.False(viewModel.IsFollowingSource);
  }
  private sealed class RecordingTopicsService : ITopicsService, IBookmarkService
  {
    public string? LastTopicId { get; private set; }

    public string? LastTopicFeedTopicId { get; private set; }

    public TopicResponse? TopicResponse { get; init; }

    public RssFeedsResponse? RssFeedsResponse { get; init; }

    public bool ThrowOnTopicFollow { get; init; }

    public bool ThrowOnSourceFollow { get; init; }

    public bool ThrowOnVoteTopic { get; init; }

    public List<(string EntityId, bool Active)> BookmarkCalls { get; } = [];

    public List<(string TopicId, bool Following)> TopicFollowCalls { get; } = [];

    public List<(string FeedId, bool Following)> SourceFollowCalls { get; } = [];

    public Task<TopicSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicResponse> FetchTopicAsync(
        string topicIdOrSlug,
        CancellationToken cancellationToken = default)
    {
      LastTopicId = topicIdOrSlug;
      return Task.FromResult(TopicResponse ?? throw new InvalidOperationException("Missing topic."));
    }

    public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
        string topicId,
        RssFeedEnabledFilter? enabled = null,
        CancellationToken cancellationToken = default)
    {
      LastTopicFeedTopicId = topicId;
      return Task.FromResult(RssFeedsResponse ?? throw new InvalidOperationException("Missing feed."));
    }

    public Task<TopicMutationResponse> CreateTopicAsync(
        CreateTopicRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMutationResponse> UpdateTopicAsync(
        string topicIdOrSlug,
        UpdateTopicBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VoteTopicAsync(
        string topicId,
        int score,
        CancellationToken cancellationToken = default)
    {
      if (ThrowOnVoteTopic) throw new InvalidOperationException("Vote failed.");
      return Task.CompletedTask;
    }

    public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default)
    {
      if (ThrowOnTopicFollow) throw new InvalidOperationException("Topic follow failed.");
      TopicFollowCalls.Add((topicId, true));
      return Task.CompletedTask;
    }

    public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default)
    {
      if (ThrowOnTopicFollow) throw new InvalidOperationException("Topic follow failed.");
      TopicFollowCalls.Add((topicId, false));
      return Task.CompletedTask;
    }

    public Task FollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default)
    {
      if (ThrowOnSourceFollow) throw new InvalidOperationException("Source follow failed.");
      SourceFollowCalls.Add((rssFeedId, true));
      return Task.CompletedTask;
    }

    public Task UnfollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default)
    {
      if (ThrowOnSourceFollow) throw new InvalidOperationException("Source follow failed.");
      SourceFollowCalls.Add((rssFeedId, false));
      return Task.CompletedTask;
    }

    public Task SetAsync(
        string entityType,
        string entityId,
        BookmarkPredicate predicate,
        bool active,
        CancellationToken cancellationToken = default)
    {
      BookmarkCalls.Add((entityId, active));
      return Task.CompletedTask;
    }

    public Task UpdateSourceAsync(
        string rssFeedId,
        UpdateRssFeedBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ListResponse<TopicAlias>> FetchTopicAliasesAsync(
        string topicId,
        string? after = null,
        int? limit = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ListResponse<TopicAdditionalHostname>> FetchTopicAdditionalHostnamesAsync(
        string topicId,
        string? after = null,
        int? limit = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task CreateTopicAliasesAsync(
        string topicId,
        CreateTopicAliasesBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteTopicAliasAsync(string topicId, string aliasValue, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicAdditionalHostnameResponse> CreateTopicAdditionalHostnameAsync(
        string topicId,
        string hostname,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteTopicAdditionalHostnameAsync(
        string topicId,
        string hostnameId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMergeResponse> MergeTopicAliasesAsync(
        string sourceTopicId,
        string destinationIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

}

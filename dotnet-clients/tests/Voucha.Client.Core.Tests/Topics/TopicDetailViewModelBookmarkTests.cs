using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicDetailViewModelBookmarkTests
{
  [Fact]
  public async Task ToggleTopicMuteAsyncRestoresStateWhenBookmarkWriteFails()
  {
    var bookmarkService = new RecordingBookmarkService(new InvalidOperationException("Bookmark failed."));
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed"),
          Bookmarks: new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["topic-1"] = new(Follow: true),
          }),
      RssFeedsResponse = RssFeeds(),
    };
    var viewModel = new TopicDetailViewModel(service, bookmarkService);

    await viewModel.LoadAsync("topic-1", followSource: false, TestContext.Current.CancellationToken);
    await viewModel.ToggleTopicMuteAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Bookmark failed.", viewModel.ErrorMessage);
    Assert.True(viewModel.IsFollowingTopic);
    Assert.False(viewModel.IsMutedTopic);
    Assert.Equal([
      ("topic", "topic-1", BookmarkPredicate.Mute, true),
    ], bookmarkService.Calls);
  }

  [Fact]
  public async Task ToggleSourceMuteAsyncRestoresStateWhenBookmarkWriteFails()
  {
    var bookmarkService = new RecordingBookmarkService(new InvalidOperationException("Bookmark failed."));
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed"),
          Bookmarks: new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["topic-1"] = new(Follow: true),
          }),
      RssFeedsResponse = RssFeeds(new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
      {
        ["feed-1"] = new(Follow: true),
      }),
    };
    var viewModel = new TopicDetailViewModel(service, bookmarkService);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.ToggleSourceMuteAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Bookmark failed.", viewModel.ErrorMessage);
    Assert.True(viewModel.IsFollowingSource);
    Assert.False(viewModel.IsMutedSource);
    Assert.Equal([
      ("rss_feed", "feed-1", BookmarkPredicate.Mute, true),
    ], bookmarkService.Calls);
  }

  [Fact]
  public async Task ToggleTopicMuteAsyncRestoresStateWithoutErrorWhenCancelled()
  {
    var bookmarkService = new RecordingBookmarkService(new OperationCanceledException());
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed"),
          Bookmarks: new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["topic-1"] = new(Follow: true),
          }),
      RssFeedsResponse = RssFeeds(),
    };
    var viewModel = new TopicDetailViewModel(service, bookmarkService);

    await viewModel.LoadAsync("topic-1", followSource: false, TestContext.Current.CancellationToken);
    await viewModel.ToggleTopicMuteAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.True(viewModel.IsFollowingTopic);
    Assert.False(viewModel.IsMutedTopic);
    Assert.Equal([
      ("topic", "topic-1", BookmarkPredicate.Mute, true),
    ], bookmarkService.Calls);
  }

  [Fact]
  public async Task ToggleSourceMuteAsyncRestoresStateWithoutErrorWhenCancelled()
  {
    var bookmarkService = new RecordingBookmarkService(new OperationCanceledException());
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed"),
          Bookmarks: new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["topic-1"] = new(Follow: true),
          }),
      RssFeedsResponse = RssFeeds(new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
      {
        ["feed-1"] = new(Follow: true),
      }),
    };
    var viewModel = new TopicDetailViewModel(service, bookmarkService);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.ToggleSourceMuteAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.True(viewModel.IsFollowingSource);
    Assert.False(viewModel.IsMutedSource);
    Assert.Equal([
      ("rss_feed", "feed-1", BookmarkPredicate.Mute, true),
    ], bookmarkService.Calls);
  }

  [Fact]
  public async Task ToggleTopicMuteAsyncDoesNothingWhenTopicIsMissing()
  {
    var service = new RecordingTopicsService();
    var viewModel = new TopicDetailViewModel(service, service);

    await viewModel.ToggleTopicMuteAsync(TestContext.Current.CancellationToken);

    Assert.Empty(service.BookmarkCalls);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleSourceMuteAsyncDoesNothingWhenSourceIsUnavailable()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(new Topic("topic-1", "Rewards", "rewards", "topic")),
    };
    var viewModel = new TopicDetailViewModel(service, service);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.ToggleSourceMuteAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanMuteSource);
    Assert.Empty(service.BookmarkCalls);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ConstructorUsesNullBookmarkServiceWhenBookmarkServiceIsOmitted()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "topic"),
          Bookmarks: new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["topic-1"] = new(Follow: true),
          }),
    };
    var viewModel = new TopicDetailViewModel(service);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.ToggleTopicMuteAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsMutedTopic);
    Assert.False(viewModel.IsFollowingTopic);
    Assert.Empty(service.BookmarkCalls);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task FollowAndVoteOperationsCaptureFailures()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed"),
          TopicElection: new TopicElection("election-1", "topic-1", 2.5, 3, 1),
          ElectionVote: new ElectionVote("topic_election", "topic-1", "user-1", ElectionVoteChoice.Like, DateTimeOffset.UnixEpoch)),
      RssFeedsResponse = RssFeeds(),
      ThrowOnTopicFollow = true,
      ThrowOnSourceFollow = true,
      ThrowOnVoteTopic = true,
    };
    var viewModel = new TopicDetailViewModel(service);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);
    await viewModel.ToggleTopicFollowAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Topic follow failed.", viewModel.ErrorMessage);
    Assert.False(viewModel.IsFollowingTopic);

    await viewModel.ToggleSourceFollowAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Source follow failed.", viewModel.ErrorMessage);
    Assert.False(viewModel.IsFollowingSource);

    await viewModel.VoteTopicAsync(ElectionVoteChoice.Dislike, TestContext.Current.CancellationToken);

    Assert.Equal("Vote failed.", viewModel.ErrorMessage);
    Assert.Equal(2.5, viewModel.VoteScoreNet);
    Assert.Equal(3, viewModel.VoteCountUp);
    Assert.Equal(1, viewModel.VoteCountDown);
    Assert.Equal(ElectionVoteChoice.Like, viewModel.CurrentVoteChoice);
  }

  [Fact]
  public async Task VoteRollsBackAndRequestsEmailRecoveryWhenVerificationIsRequired()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "topic"),
          TopicElection: new TopicElection("election-1", "topic-1", 2.5, 3, 1),
          ElectionVote: new ElectionVote("topic_election", "topic-1", "user-1", ElectionVoteChoice.Like, DateTimeOffset.UnixEpoch)),
      VoteException = new VouchaApiException(
          System.Net.HttpStatusCode.Forbidden,
          "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}"),
    };
    var viewModel = new TopicDetailViewModel(service);
    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);

    await viewModel.VoteTopicAsync(ElectionVoteChoice.Dislike, TestContext.Current.CancellationToken);

    Assert.Equal(2.5, viewModel.VoteScoreNet);
    Assert.Equal(ElectionVoteChoice.Like, viewModel.CurrentVoteChoice);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
    Assert.False(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  private static RssFeedsResponse RssFeeds(
      IReadOnlyDictionary<string, BookmarkPredicates>? bookmarks = null) =>
      new(
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
          bookmarks,
          null,
          null);

  private sealed class RecordingTopicsService : ITopicsService, IBookmarkService
  {
    public TopicResponse? TopicResponse { get; init; }

    public RssFeedsResponse? RssFeedsResponse { get; init; }

    public bool ThrowOnTopicFollow { get; init; }

    public bool ThrowOnSourceFollow { get; init; }

    public bool ThrowOnVoteTopic { get; init; }

    public Exception? VoteException { get; init; }

    public List<(string EntityId, bool Active)> BookmarkCalls { get; } = [];

    public Task<TopicSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicResponse> FetchTopicAsync(
        string topicIdOrSlug,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(TopicResponse ?? throw new InvalidOperationException("Missing topic."));

    public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
        string topicId,
        RssFeedEnabledFilter? enabled = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RssFeedsResponse ?? throw new InvalidOperationException("Missing feed."));

    public Task<TopicMutationResponse> CreateTopicAsync(
        CreateTopicRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMutationResponse> UpdateTopicAsync(
        string topicIdOrSlug,
        UpdateTopicBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VoteTopicAsync(string topicId, ElectionVoteChoice choice, CancellationToken cancellationToken = default) =>
        VoteException is not null
            ? Task.FromException(VoteException)
            : ThrowOnVoteTopic
                ? Task.FromException(new InvalidOperationException("Vote failed."))
                : Task.CompletedTask;

    public Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default) =>
        VoteTopicAsync(topicId, ElectionVoteChoice.Neutral, cancellationToken);

    public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        ThrowOnTopicFollow
            ? Task.FromException(new InvalidOperationException("Topic follow failed."))
            : Task.CompletedTask;

    public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        ThrowOnTopicFollow
            ? Task.FromException(new InvalidOperationException("Topic follow failed."))
            : Task.CompletedTask;

    public Task FollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        ThrowOnSourceFollow
            ? Task.FromException(new InvalidOperationException("Source follow failed."))
            : Task.CompletedTask;

    public Task UnfollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        ThrowOnSourceFollow
            ? Task.FromException(new InvalidOperationException("Source follow failed."))
            : Task.CompletedTask;

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

    public Task<ListResponse<string>> FetchTopicAliasesAsync(
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

  private sealed class RecordingBookmarkService(Exception? exceptionToThrow = null) : IBookmarkService
  {
    public List<(string EntityType, string EntityId, BookmarkPredicate Predicate, bool Active)> Calls { get; } = [];

    public Task SetAsync(
        string entityType,
        string entityId,
        BookmarkPredicate predicate,
        bool active,
        CancellationToken cancellationToken = default)
    {
      Calls.Add((entityType, entityId, predicate, active));
      return exceptionToThrow is not null ? Task.FromException(exceptionToThrow) : Task.CompletedTask;
    }
  }
}

using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicDetailViewModelPropertyChangedTests
{
  [Fact]
  public async Task LoadAsyncRaisesDependentNotificationsForTopicAndSourceBindings()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-1", "Rewards", "rewards", "rss_feed"),
          Bookmarks: new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
          {
            ["topic-1"] = new(Follow: true, Mute: true),
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
            ["feed-1"] = new(Follow: true, Mute: true),
          },
          null,
          null),
    };
    var viewModel = new TopicDetailViewModel(service);
    var changed = new List<string?>();
    viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

    await viewModel.LoadAsync("topic-1", TestContext.Current.CancellationToken);

    Assert.Contains(nameof(TopicDetailViewModel.Topic), changed);
    Assert.Contains(nameof(TopicDetailViewModel.CanMuteTopic), changed);
    Assert.Contains(nameof(TopicDetailViewModel.CanFollowSource), changed);
    Assert.Contains(nameof(TopicDetailViewModel.CanMuteSource), changed);
    Assert.Contains(nameof(TopicDetailViewModel.TopicFollowActionLabel), changed);
    Assert.Contains(nameof(TopicDetailViewModel.TopicMuteActionLabel), changed);
    Assert.Contains(nameof(TopicDetailViewModel.SourceFollowActionLabel), changed);
    Assert.Contains(nameof(TopicDetailViewModel.SourceMuteActionLabel), changed);
    Assert.Contains(nameof(TopicDetailViewModel.SourceRssFeedId), changed);
  }

  private sealed class RecordingTopicsService : ITopicsService
  {
    public TopicResponse? TopicResponse { get; init; }

    public RssFeedsResponse? RssFeedsResponse { get; init; }

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
        Task.FromResult(RssFeedsResponse ?? throw new InvalidOperationException("Missing feeds."));

    public Task<TopicMutationResponse> CreateTopicAsync(
        CreateTopicRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMutationResponse> UpdateTopicAsync(
        string topicIdOrSlug,
        UpdateTopicBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task FollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UnfollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SetAsync(
        string entityType,
        string entityId,
        BookmarkPredicate predicate,
        bool active,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

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
}

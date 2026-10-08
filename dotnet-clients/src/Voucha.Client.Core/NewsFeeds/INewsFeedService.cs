using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NewsFeeds;

public sealed record NewsFeedPage(
    IReadOnlyList<NewsFeedItem> Items,
    PageInfo PageInfo,
    IReadOnlyDictionary<string, string>? StoryPostIds = null);

public interface INewsFeedService
{
  Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
      NewsFeedScope scope,
      CancellationToken cancellationToken = default);

  async Task<NewsFeedPage> GetNewsFeedPageAsync(
      NewsFeedScope scope,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default) =>
      new(
          await GetNewsFeedItemsAsync(scope, cancellationToken).ConfigureAwait(false),
          new PageInfo(null, false, null));

  async Task<NewsFeedPage> GetNewsFeedPageAsync(
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default) =>
      new(
          await GetNewsFeedItemsAsync(scope, sourceFeedType, cancellationToken).ConfigureAwait(false),
          new PageInfo(null, false, null));

  Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType,
      CancellationToken cancellationToken = default);

  Task SetSourceFollowAsync(
      string sourceId,
      bool following,
      CancellationToken cancellationToken = default);

  Task SetTopicFollowAsync(
      string topicId,
      bool following,
      CancellationToken cancellationToken = default);

  Task SetReadAsync(
      string itemId,
      bool read,
      CancellationToken cancellationToken = default);

  Task VoteRssFeedItemAsync(
      string itemId,
      Api.ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Semantic news voting is not implemented by this news service."));

  Task ClearRssFeedItemVoteAsync(string itemId, CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Clearing a news vote is not implemented by this news service."));

  Task VoteTopicAsync(
      string topicId,
      Api.ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Semantic topic voting is not implemented by this news service."));

  Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Clearing a topic vote is not implemented by this news service."));
}

public interface INewsFeedSessionState
{
  void ResetSessionState();
}

public interface IStoryDiscussionService
{
  Task<StoryDiscussionResult> CreateStoryDiscussionAsync(
      string storyId,
      string fallbackRssFeedItemId,
      CancellationToken cancellationToken = default);
}

public sealed record StoryDiscussionResult(string PostId);

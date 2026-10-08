using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.Core.Tests.NewsFeeds;

internal sealed class HeldContinuationFeedService(NewsFeedItem primary) : INewsFeedService, IStoryRelatedArticlesService
{
  private readonly TaskCompletionSource<NewsFeedPage> continuation = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private bool initialReturned;

  public TaskCompletionSource ContinuationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

  public string? StoryContinuationCursor { get; private set; }

  public void ReleaseContinuation(NewsFeedPage page) => continuation.TrySetResult(page);

  public Task<NewsFeedPage> GetNewsFeedPageAsync(
      NewsFeedScope scope,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default)
  {
    if (!initialReturned)
    {
      initialReturned = true;
      return Task.FromResult(new NewsFeedPage([primary], new("cursor-1", true, null)));
    }

    ContinuationStarted.TrySetResult();
    return continuation.Task.WaitAsync(cancellationToken);
  }

  public Task<NewsFeedPage> GetStoryRelatedArticlesPageAsync(
      string storyId,
      string primaryItemId,
      string? after,
      CancellationToken cancellationToken = default)
  {
    StoryContinuationCursor = after;
    return Task.FromResult(new NewsFeedPage([], new(null, false, null)));
  }

  public Task<NewsFeedPage> GetNewsFeedPageAsync(
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default) =>
      GetNewsFeedPageAsync(scope, after, limit, cancellationToken);

  public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);

  public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType,
      CancellationToken cancellationToken = default) =>
      GetNewsFeedItemsAsync(scope, cancellationToken);

  public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
  public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
  public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) => Task.CompletedTask;
  public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
  public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class HeldBookmarkService : IBookmarkService
{
  private readonly TaskCompletionSource<Exception> result = new(TaskCreationOptions.RunContinuationsAsynchronously);

  public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

  public void FailHide(Exception error) => result.TrySetResult(error);

  public async Task SetAsync(
      string entityType,
      string entityId,
      BookmarkPredicate predicate,
      bool active,
      CancellationToken cancellationToken = default)
  {
    RequestStarted.TrySetResult();
    var error = await result.Task.WaitAsync(cancellationToken);
    throw error;
  }
}

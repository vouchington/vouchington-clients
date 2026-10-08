using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelSuccessfulHideFeedRaceTests
{
  [Fact]
  public async Task FeedPageCompletingWhilePrimaryHideIsPendingDoesNotRestorePrimary()
  {
    var service = new HeldFeedService(Primary());
    var bookmarks = new HeldBookmarkService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? hide = null;
    Task? continuation = null;

    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      hide = viewModel.ToggleHideAsync(viewModel.Items[0], cancellation.Token);
      await bookmarks.RequestStarted.Task.WaitAsync(cancellation.Token);
      continuation = viewModel.LoadMoreAsync(cancellation.Token);
      await service.ContinuationStarted.Task.WaitAsync(cancellation.Token);

      service.ReleaseContinuation(Page(Primary(), Item("independent", null)));
      await continuation;

      Assert.DoesNotContain(viewModel.Items, item => item.Id == "primary");
      Assert.Equal(["independent"], viewModel.Items.Select(item => item.Id));

      bookmarks.CompleteHide();
      await hide;
      Assert.DoesNotContain(viewModel.Items, item => item.Id == "primary");
      Assert.Equal(["independent"], viewModel.Items.Select(item => item.Id));
    }
    finally
    {
      cancellation.Cancel();
      service.ReleaseContinuation(Page());
      bookmarks.FailHide(new OperationCanceledException("Test cleanup."));
      await CompleteWhenStarted(hide);
      await CompleteWhenStarted(continuation);
    }
  }

  [Fact]
  public async Task FeedPageAlreadyInFlightCannotRestorePrimaryAfterSuccessfulHide()
  {
    var service = new HeldFeedService(Primary());
    var bookmarks = new HeldBookmarkService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? hide = null;
    Task? continuation = null;

    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      continuation = viewModel.LoadMoreAsync(cancellation.Token);
      await service.ContinuationStarted.Task.WaitAsync(cancellation.Token);
      hide = viewModel.ToggleHideAsync(viewModel.Items[0], cancellation.Token);
      await bookmarks.RequestStarted.Task.WaitAsync(cancellation.Token);
      bookmarks.CompleteHide();
      await hide;

      service.ReleaseContinuation(Page(Primary(), Item("independent", null)));
      await continuation;

      Assert.DoesNotContain(viewModel.Items, item => item.Id == "primary");
      Assert.Equal(["independent"], viewModel.Items.Select(item => item.Id));
    }
    finally
    {
      cancellation.Cancel();
      service.ReleaseContinuation(Page());
      bookmarks.FailHide(new OperationCanceledException("Test cleanup."));
      await CompleteWhenStarted(hide);
      await CompleteWhenStarted(continuation);
    }
  }

  [Fact]
  public async Task ExplicitUnhideClearsSuppressionForLaterFeedPages()
  {
    var service = new HeldFeedService(Primary());
    var bookmarks = new HeldBookmarkService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

    await viewModel.LoadAsync(cancellation.Token);
    bookmarks.CompleteHide();
    await viewModel.ToggleHideAsync(viewModel.Items[0], cancellation.Token);
    Assert.DoesNotContain(viewModel.Items, item => item.Id == "primary");

    await viewModel.ToggleHideAsync(Primary() with { IsHidden = true }, cancellation.Token);
    service.ReleaseContinuation(Page(Primary()));
    await viewModel.LoadMoreAsync(cancellation.Token);

    Assert.Single(viewModel.Items, item => item.Id == "primary");
  }

  [Fact]
  public async Task ReloadStartsANewSuppressionGeneration()
  {
    var service = new HeldFeedService(Primary(), Page());
    var bookmarks = new HeldBookmarkService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

    await viewModel.LoadAsync(cancellation.Token);
    bookmarks.CompleteHide();
    await viewModel.ToggleHideAsync(viewModel.Items[0], cancellation.Token);
    Assert.DoesNotContain(viewModel.Items, item => item.Id == "primary");

    await viewModel.LoadAsync(cancellation.Token);
    Assert.Empty(viewModel.Items);
    service.ReleaseContinuation(Page(Primary()));
    await viewModel.LoadMoreAsync(cancellation.Token);

    Assert.Single(viewModel.Items, item => item.Id == "primary");
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task FailedOrCancelledHideClearsSuppressionAndRestoresOriginalStory(bool cancelHide)
  {
    var localization = UiLocalization.English;
    var originalGroup = new StoryRelatedArticles(
        "story-1", "primary", [Item("original-peer", "story-1")], new("original-cursor", true, null), localization);
    originalGroup.SuppressHiddenPeer("suppressed-peer");
    var primary = Primary() with { StoryArticles = originalGroup };
    var service = new HeldFeedService(primary);
    var bookmarks = new HeldBookmarkService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? hide = null;
    Task? continuation = null;

    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      hide = viewModel.ToggleHideAsync(viewModel.Items[0], cancellation.Token);
      await bookmarks.RequestStarted.Task.WaitAsync(cancellation.Token);
      continuation = viewModel.LoadMoreAsync(cancellation.Token);
      await service.ContinuationStarted.Task.WaitAsync(cancellation.Token);

      service.ReleaseContinuation(Page(
          Primary() with
          {
            StoryArticles = new StoryRelatedArticles(
                "story-1", "primary", [Item("new-peer", "story-1"), Item("suppressed-peer", "story-1")],
                new("incoming-cursor", true, null), localization),
          },
          Item("independent", null)));
      await continuation;

      Assert.DoesNotContain(viewModel.Items, item => item.Id == "primary");
      Assert.Contains(viewModel.Items, item => item.Id == "independent");

      if (cancelHide) bookmarks.FailHide(new OperationCanceledException("Hide cancelled."));
      else bookmarks.FailHide(new InvalidOperationException("Hide failed."));
      await hide;

      var restored = Assert.Single(viewModel.Items, item => item.Id == "primary");
      Assert.Same(originalGroup, restored.StoryArticles);
      Assert.Equal(["new-peer", "original-peer"], originalGroup.Items.Select(item => item.Id).Order(StringComparer.Ordinal));
      Assert.DoesNotContain(originalGroup.Items, item => item.Id == "suppressed-peer");
      Assert.Equal(["primary"], viewModel.Items.Where(item => item.StoryId == "story-1").Select(item => item.Id));
      Assert.Equal(["primary", "independent"], viewModel.Items.Select(item => item.Id));
      await viewModel.LoadMoreStoryArticlesAsync(restored, cancellation.Token);
      Assert.Equal("original-cursor", service.StoryContinuationCursor);
      Assert.Equal(cancelHide ? null : "Hide failed.", viewModel.ErrorMessage);
    }
    finally
    {
      cancellation.Cancel();
      service.ReleaseContinuation(Page());
      bookmarks.FailHide(new OperationCanceledException("Test cleanup."));
      await CompleteWhenStarted(hide);
      await CompleteWhenStarted(continuation);
    }
  }

  private static NewsFeedItem Primary() => Item("primary", "story-1");

  private static NewsFeedItem Item(string id, string? storyId) =>
      new(id, id, "Source", "Summary", null, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), StoryId: storyId);

  private static NewsFeedPage Page(params NewsFeedItem[] items) => new(items, new("next", true, null));

  private static async Task CompleteWhenStarted(Task? task)
  {
    if (task is null) return;
    try { await task; }
    catch (OperationCanceledException) { }
    catch (Exception) { }
  }

  private sealed class HeldFeedService : INewsFeedService, IStoryRelatedArticlesService
  {
    private readonly Queue<NewsFeedPage> initialPages;
    private readonly TaskCompletionSource<NewsFeedPage> continuation = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public HeldFeedService(NewsFeedItem primary, params NewsFeedPage[] laterInitialPages) =>
        initialPages = new([Page(primary), .. laterInitialPages]);

    public TaskCompletionSource ContinuationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string? StoryContinuationCursor { get; private set; }
    public void ReleaseContinuation(NewsFeedPage page) => continuation.TrySetResult(page);

    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, string? after = null, int limit = 20, CancellationToken cancellationToken = default)
    {
      if (after is null && initialPages.TryDequeue(out var initialPage))
      {
        return Task.FromResult(initialPage);
      }
      ContinuationStarted.TrySetResult();
      return continuation.Task.WaitAsync(cancellationToken);
    }

    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType, string? after = null, int limit = 20, CancellationToken cancellationToken = default) =>
        GetNewsFeedPageAsync(scope, after, limit, cancellationToken);
    public Task<NewsFeedPage> GetStoryRelatedArticlesPageAsync(string storyId, string primaryItemId, string? after, CancellationToken cancellationToken = default)
    {
      StoryContinuationCursor = after;
      return Task.FromResult(Page());
    }
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType, CancellationToken cancellationToken = default) => GetNewsFeedItemsAsync(scope, cancellationToken);
    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class HeldBookmarkService : IBookmarkService
  {
    private readonly TaskCompletionSource<Exception?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void CompleteHide() => result.TrySetResult(null);
    public void FailHide(Exception error) => result.TrySetResult(error);
    public async Task SetAsync(string entityType, string entityId, BookmarkPredicate predicate, bool active, CancellationToken cancellationToken = default)
    {
      RequestStarted.TrySetResult();
      var error = await result.Task.WaitAsync(cancellationToken);
      if (error is not null) throw error;
    }
  }

}

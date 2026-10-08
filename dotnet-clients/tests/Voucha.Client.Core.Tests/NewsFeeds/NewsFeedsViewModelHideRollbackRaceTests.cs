using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelHideRollbackRaceTests
{
  [Fact]
  public async Task FailedPrimaryHideRestoresInterleavedShareDeliveries()
  {
    var primary = Item("article", "story-1") with { DeliveryId = "direct" };
    var firstShare = Item("article", null) with { DeliveryId = "share-1" };
    var secondShare = Item("article", null) with { DeliveryId = "share-2" };
    var feed = new HeldContinuationFeedService(primary, firstShare, Item("other", null), secondShare);
    var bookmarks = new HeldBookmarkService();
    var model = new NewsFeedsViewModel(feed, NewsFeedScope.AllNews, bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? hide = null;
    try
    {
      await model.LoadAsync(cancellation.Token);
      Assert.Equal(["direct", "share-1", "other", "share-2"], model.Items.Select(item => item.FeedRowId));

      hide = model.ToggleHideAsync(model.Items[0], cancellation.Token);
      await bookmarks.RequestStarted.Task.WaitAsync(cancellation.Token);
      Assert.Equal(["other"], model.Items.Select(item => item.FeedRowId));

      bookmarks.FailHide(new InvalidOperationException("Hide failed."));
      await hide;
      Assert.Equal(["direct", "share-1", "other", "share-2"], model.Items.Select(item => item.FeedRowId));
      Assert.Equal("Hide failed.", model.ErrorMessage);
    }
    finally
    {
      cancellation.Cancel();
      bookmarks.FailHide(new OperationCanceledException("Test cleanup."));
      await CompleteWhenStarted(hide);
    }
  }

  [Fact]
  public void StoryRollbackUsesIncomingContinuationOnlyWhenOriginalGroupIsExhausted()
  {
    var localization = UiLocalization.English;
    var original = new StoryRelatedArticles("story-1", "primary", [], new("original-cursor", true, null), localization);
    var incoming = new StoryRelatedArticles("story-1", "primary", [], new("incoming-cursor", true, null), localization);

    original.IncludeContinuation(incoming);
    var originalRequest = original.BeginNextPage();
    Assert.Equal("original-cursor", originalRequest?.Cursor);

    var exhausted = new StoryRelatedArticles("story-2", "primary-2", [], new(null, false, null), localization);
    var replacement = new StoryRelatedArticles("story-2", "primary-2", [], new("replacement-cursor", true, null), localization);

    exhausted.IncludeContinuation(replacement);
    var replacementRequest = exhausted.BeginNextPage();
    Assert.Equal("replacement-cursor", replacementRequest?.Cursor);
  }

  [Fact]
  public async Task FailedPrimaryHideMergesContinuationRowsUnderOriginalStoryPrimary()
  {
    var localization = UiLocalization.English;
    var originalGroup = new StoryRelatedArticles(
        "story-1",
        "primary",
        [Item("original-peer", "story-1")],
        new(null, false, null),
        localization);
    originalGroup.SuppressHiddenPeer("suppressed-peer");
    var primary = Item("primary", "story-1") with { StoryArticles = originalGroup };
    var feed = new HeldContinuationFeedService(primary);
    var bookmarks = new HeldBookmarkService();
    var viewModel = new NewsFeedsViewModel(feed, NewsFeedScope.AllNews, bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? hide = null;
    Task? continuation = null;

    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      hide = viewModel.ToggleHideAsync(viewModel.Items[0], cancellation.Token);
      await bookmarks.RequestStarted.Task.WaitAsync(cancellation.Token);

      continuation = viewModel.LoadMoreAsync(cancellation.Token);
      await feed.ContinuationStarted.Task.WaitAsync(cancellation.Token);
      feed.ReleaseContinuation(new NewsFeedPage(
          [
          Item("unrelated", null),
            Item("unrelated-later", null),
            Item("primary", "story-1") with
            {
              StoryArticles = new StoryRelatedArticles(
                  "story-1",
                  "primary",
                  [Item("new-group-peer", "story-1"), Item("suppressed-peer", "story-1")],
                  new("incoming-cursor", true, null),
                  localization),
            },
            Item("later-peer", "story-1"),
          ],
          new(null, false, null)));
      await continuation;

      bookmarks.FailHide(new InvalidOperationException("Hide failed."));
      await hide;

      var restored = Assert.Single(viewModel.Items, item => item.Id == "primary");
      Assert.False(restored.IsHidden);
      Assert.Same(originalGroup, restored.StoryArticles);
      Assert.Equal(
          ["later-peer", "new-group-peer", "original-peer"],
          originalGroup.Items.Select(item => item.Id).Order(StringComparer.Ordinal));
      Assert.DoesNotContain(originalGroup.Items, item => item.Id == "suppressed-peer");
      Assert.Equal(["primary"], viewModel.Items.Where(item => item.StoryId == "story-1").Select(item => item.Id));
      Assert.Equal(["primary", "unrelated", "unrelated-later"], viewModel.Items.Select(item => item.Id));
      Assert.Equal("Hide failed.", viewModel.ErrorMessage);
      Assert.True(originalGroup.HasMore);

      await viewModel.LoadMoreStoryArticlesAsync(restored, cancellation.Token);

      Assert.Equal("incoming-cursor", feed.StoryContinuationCursor);
    }
    finally
    {
      cancellation.Cancel();
      feed.ReleaseContinuation(new NewsFeedPage([], new(null, false, null)));
      bookmarks.FailHide(new OperationCanceledException("Test cleanup."));
      await CompleteWhenStarted(hide);
      await CompleteWhenStarted(continuation);
    }
  }

  private static NewsFeedItem Item(string id, string? storyId) =>
      new(id, id, "Source", "Summary", null, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), StoryId: storyId);

  private static async Task CompleteWhenStarted(Task? task)
  {
    if (task is null) return;
    try
    {
      await task;
    }
    catch (OperationCanceledException)
    {
      // The test owns cancellation during cleanup.
    }
    catch (Exception)
    {
      // Preserve the assertion that failed before cleanup began.
    }
  }

}

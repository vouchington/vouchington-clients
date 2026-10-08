using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed partial class StoryRelatedArticlesTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task HiddenPeerStaysOutOfDelayedFeedPage(bool finishHideBeforePage)
  {
    var group = Group(1);
    var feedPage = new TaskCompletionSource<NewsFeedPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var bookmarkService = new PendingBookmarkService();
    var service = new Service(new([Primary(group)], new("feed-after", true, null)));
    var model = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarkService);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.FeedResponse = feedPage.Task;

    var pendingPage = model.LoadMoreAsync(TestContext.Current.CancellationToken);
    var pendingHide = model.ToggleHideAsync(group.Items[0], TestContext.Current.CancellationToken);
    try
    {
      Assert.Empty(group.Items);
      if (finishHideBeforePage)
      {
        bookmarkService.Completion.TrySetResult();
        await pendingHide;
      }
      feedPage.TrySetResult(new([
        Item("peer-1") with { StoryId = "story-1" },
        Item("peer-2") with { StoryId = "story-1" }
      ], new(null, false, null)));
      await pendingPage;
      Assert.Equal(["peer-2"], group.Items.Select(item => item.Id));
      bookmarkService.Completion.TrySetResult();
      await pendingHide;
      Assert.Equal(["peer-2"], group.Items.Select(item => item.Id));
      Assert.Single(model.Items);
    }
    finally
    {
      feedPage.TrySetResult(new([], new(null, false, null)));
      bookmarkService.Completion.TrySetResult();
      await Task.WhenAll(pendingPage, pendingHide);
    }
  }

  [Fact]
  public async Task FailedHideRestoresPeerAfterDelayedFeedPage()
  {
    var group = Group(1);
    var feedPage = new TaskCompletionSource<NewsFeedPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var bookmarkService = new PendingBookmarkService();
    var service = new Service(new([Primary(group)], new("feed-after", true, null)));
    var model = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarkService);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.FeedResponse = feedPage.Task;

    var pendingPage = model.LoadMoreAsync(TestContext.Current.CancellationToken);
    var pendingHide = model.ToggleHideAsync(group.Items[0], TestContext.Current.CancellationToken);
    try
    {
      feedPage.TrySetResult(new([Item("peer-1") with { StoryId = "story-1" }], new(null, false, null)));
      await pendingPage;
      Assert.Empty(group.Items);
      bookmarkService.Completion.TrySetException(new InvalidOperationException("Hide failed."));
      await pendingHide;
      Assert.Equal(["peer-1"], group.Items.Select(item => item.Id));
      Assert.False(group.Items[0].IsHidden);
    }
    finally
    {
      feedPage.TrySetResult(new([], new(null, false, null)));
      bookmarkService.Completion.TrySetException(new InvalidOperationException("Hide failed."));
      await Task.WhenAll(pendingPage, pendingHide);
    }
  }

  [Fact]
  public async Task FailedPeerHideRestoresDetachedGroupBeforePrimaryRollback()
  {
    var group = Group(1);
    var bookmarks = new SeparateBookmarkService();
    var service = new Service(new([Primary(group)], new(null, false, null)));
    var model = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarks);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    var peerHide = model.ToggleHideAsync(group.Items[0], TestContext.Current.CancellationToken);
    var primaryHide = model.ToggleHideAsync(model.Items[0], TestContext.Current.CancellationToken);
    try
    {
      Assert.Empty(model.Items);
      Assert.Empty(group.Items);
      bookmarks.Peer.TrySetException(new InvalidOperationException("Peer hide failed."));
      await peerHide;
      Assert.Equal(["peer-1"], group.Items.Select(item => item.Id));
      bookmarks.Primary.TrySetException(new InvalidOperationException("Primary hide failed."));
      await primaryHide;
      Assert.Same(group, Assert.Single(model.Items).StoryArticles);
      Assert.Equal(["peer-1"], group.Items.Select(item => item.Id));
    }
    finally
    {
      bookmarks.Peer.TrySetException(new InvalidOperationException("Peer hide failed."));
      bookmarks.Primary.TrySetException(new InvalidOperationException("Primary hide failed."));
      await Task.WhenAll(peerHide, primaryHide);
    }
  }

}

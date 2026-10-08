using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class StoryRelatedArticlesTests
{
  [Fact]
  public void ContinuationOnlyPreviewStillAllowsStoryDiscussion()
  {
    var group = new StoryRelatedArticles("story-1", "primary", [], new("after", true, null), UiLocalization.English);
    var primary = Primary(group);

    Assert.Empty(group.Items);
    Assert.True(group.HasMore);
    Assert.True(primary.CanStartStoryDiscussion);
  }

  [Fact]
  public void HiddenOnlyExhaustedPreviewHasNoVisibleExpansionOrCount()
  {
    var hidden = Item("hidden") with { IsHidden = true };
    var group = new StoryRelatedArticles("story-1", "primary", [hidden], new(null, false, null), UiLocalization.English);

    Assert.Single(group.Items);
    Assert.Empty(group.VisibleItems);
    Assert.False(group.HasItems);
    Assert.False(group.CanExpand);
    Assert.Equal("0 related articles", group.CountLabel);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(3)]
  public async Task PrefetchedExpansionNeedsNoRequest(int previewCount)
  {
    var group = Group(previewCount);
    var service = new Service(new([Primary(group)], new("feed-after", true, null)));
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    NewsFeedsViewModel.ToggleStoryArticles(model.Items[0]);

    Assert.True(group.IsExpanded);
    Assert.Equal(previewCount, group.Items.Count);
    Assert.Equal($"{previewCount}+ related articles", group.CountLabel);
    Assert.Empty(service.StoryCursors);
    Assert.True(model.Items[0].CanStartStoryDiscussion);
    Assert.Single(model.Items);
  }

  [Fact]
  public async Task ContinuationPreservesPreviewOnFailureAndRetry()
  {
    var group = Group(1);
    var service = new Service(new([Primary(group)], new(null, false, null)))
    { StoryResponse = Task.FromException<NewsFeedPage>(new HttpRequestException("Offline")) };
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.True(group.HasError);
    Assert.Single(group.Items);
    Assert.Equal("1+ related articles", group.CountLabel);
    service.StoryResponse = Task.FromResult(new NewsFeedPage([Item("peer-1"), Item("peer-2"), Item("primary")], new(null, false, null)));
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);

    Assert.Equal(["peer-1", "peer-2"], group.Items.Select(item => item.Id));
    Assert.Equal(["opaque+/=", "opaque+/="], service.StoryCursors);
    Assert.Equal("2 related articles", group.CountLabel);
    Assert.False(group.HasMore);
    Assert.False(group.HasError);
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal(2, service.StoryCursors.Count);
  }

  [Fact]
  public async Task RepeatedStoryPreservesPrimaryAndContinuation()
  {
    var group = Group(1);
    var replacement = Group(3);
    var service = new Service(new([Primary(group)], new("feed-after", true, null)));
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    group.IsExpanded = true;
    service.Feed = new([Primary(replacement) with { Id = "later-primary" }, Item("shared")], new(null, false, null));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["primary", "shared"], model.Items.Select(item => item.Id));
    Assert.Same(group, model.Items[0].StoryArticles);
    Assert.Equal(["peer-1", "later-primary", "peer-2", "peer-3"], group.Items.Select(item => item.Id));
    Assert.True(group.IsExpanded);
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal(["opaque+/="], service.StoryCursors);
  }

  [Fact]
  public async Task ContinuationKeepsDistinctStoryMembersWithoutAUsablePreview()
  {
    var service = new Service(new([Item("other")], new("feed-after", true, null)));
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.Feed = new([Item("first") with { StoryId = "story-1" }, Item("second") with { StoryId = "story-1" }],
        new(null, false, null));

    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["other", "first", "second"], model.Items.Select(item => item.Id));
    Assert.All(model.Items.Skip(1), item => Assert.Null(item.StoryArticles));
  }

  [Fact]
  public async Task LaterPreviewPromotesDisplayedMemberWithoutLosingStoryPeers()
  {
    var first = Item("first") with { StoryId = "story-1", IsSaved = true, VoteScoreNet = 2 };
    var earlierPeer = Item("earlier") with { StoryId = "story-1" };
    var service = new Service(new([first, earlierPeer], new("feed-after", true, null)));
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var laterGroup = new StoryRelatedArticles("story-1", "second", [Item("third")],
        new("story-after", true, null), UiLocalization.English);
    service.Feed = new([Item("second") with { StoryId = "story-1", StoryArticles = laterGroup }],
        new("next-feed", true, null));

    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["first"], model.Items.Select(item => item.Id));
    Assert.True(model.Items[0].IsSaved);
    Assert.Equal(2, model.Items[0].VoteScoreNet);
    var promoted = Assert.IsType<StoryRelatedArticles>(model.Items[0].StoryArticles);
    Assert.Equal("first", promoted.PrimaryItemId);
    Assert.Equal(["second", "earlier", "third"], promoted.Items.Select(item => item.Id));
    Assert.True(promoted.HasMore);
    service.Feed = new([Item("fourth") with { StoryId = "story-1" }], new(null, false, null));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["first"], model.Items.Select(item => item.Id));
    Assert.Equal(["second", "earlier", "third", "fourth"], promoted.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task IncomingStoryMemberInvalidatesHeldRelatedPageAndRetainsCursor()
  {
    var group = Group(1);
    var heldPage = new TaskCompletionSource<NewsFeedPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new Service(new([Primary(group)], new("feed-after", true, null))) { StoryResponse = heldPage.Task };
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var pending = model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    try
    {
      Assert.True(group.IsLoading);
      service.Feed = new([Item("feed-peer") with { StoryId = "story-1" }], new(null, false, null));
      await model.LoadMoreAsync(TestContext.Current.CancellationToken);
      Assert.False(group.IsLoading);
      Assert.Equal(["peer-1", "feed-peer"], group.Items.Select(item => item.Id));
    }
    finally
    {
      heldPage.TrySetResult(new([Item("stale-peer")], new("stale-cursor", true, null)));
      await pending;
    }
    Assert.Equal(["peer-1", "feed-peer"], group.Items.Select(item => item.Id));
    Assert.True(group.HasMore);
    service.StoryResponse = Task.FromResult(new NewsFeedPage([Item("fresh-peer")], new(null, false, null)));
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal(["opaque+/=", "opaque+/="], service.StoryCursors);
    Assert.Equal(["peer-1", "feed-peer", "fresh-peer"], group.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task ResetRejectsDelayedContinuation()
  {
    var group = Group(1);
    var completion = new TaskCompletionSource<NewsFeedPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new Service(new([Primary(group)], new(null, false, null))) { StoryResponse = completion.Task };
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var request = model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.True(group.IsLoading);
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Single(service.StoryCursors);
    service.Feed = new([Item("fresh")], new(null, false, null));
    await model.ReloadAfterSessionChangedAsync(false, TestContext.Current.CancellationToken);
    completion.SetResult(new([Item("late")], new(null, false, null)));
    await request;

    Assert.Equal(["fresh"], model.Items.Select(item => item.Id));
    Assert.Equal(["peer-1"], group.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task HidingPeerInvalidatesDelayedContinuationAndPreservesCursorForRetry()
  {
    var group = Group(1);
    var completion = new TaskCompletionSource<NewsFeedPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new Service(new([Primary(group)], new(null, false, null))) { StoryResponse = completion.Task };
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    var pending = model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    try
    {
      Assert.True(group.IsLoading);
      await model.ToggleHideAsync(group.Items[0], TestContext.Current.CancellationToken);
      Assert.Empty(group.Items);
    }
    finally
    {
      completion.TrySetResult(new([Item("peer-1"), Item("peer-2")], new("next", true, null)));
    }
    await pending;
    Assert.Empty(group.Items);
    Assert.False(group.IsLoading);
    Assert.True(group.HasMore);

    service.StoryResponse = Task.FromResult(new NewsFeedPage([Item("peer-2")], new(null, false, null)));
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal(["opaque+/=", "opaque+/="], service.StoryCursors);
    Assert.Equal(["peer-2"], group.Items.Select(item => item.Id));
  }

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

  [Fact]
  public async Task DiscardedPageAfterPrimaryHideAllowsRetryWhenHideRollsBack()
  {
    var group = Group(1);
    var pageCompletion = new TaskCompletionSource<NewsFeedPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var bookmarkService = new PendingBookmarkService();
    var service = new Service(new([Primary(group)], new(null, false, null))) { StoryResponse = pageCompletion.Task };
    var model = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarkService);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    var pageRequest = model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    var hideRequest = model.ToggleHideAsync(model.Items[0], TestContext.Current.CancellationToken);
    try
    {
      Assert.True(group.IsLoading);
      Assert.Empty(model.Items);
      pageCompletion.TrySetResult(new([Item("peer-2")], new("next", true, null)));
      await pageRequest;
      bookmarkService.Completion.TrySetException(new InvalidOperationException("Hide failed."));
      await hideRequest;
    }
    finally
    {
      pageCompletion.TrySetResult(new([], new(null, false, null)));
      bookmarkService.Completion.TrySetException(new InvalidOperationException("Hide failed."));
    }

    Assert.Single(model.Items);
    Assert.False(group.IsLoading);
    Assert.True(group.HasMore);
    service.StoryResponse = Task.FromResult(new NewsFeedPage([Item("peer-3")], new(null, false, null)));
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal(["opaque+/=", "opaque+/="], service.StoryCursors);
    Assert.Equal(["peer-1", "peer-3"], group.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task CancellationRetainsPreviewAndAllowsExplicitRetry()
  {
    var group = Group(1);
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    var service = new Service(new([Primary(group)], new(null, false, null)))
    { StoryResponse = Task.FromCanceled<NewsFeedPage>(cancelled.Token) };
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreStoryArticlesAsync(model.Items[0], cancelled.Token);
    Assert.False(group.IsLoading);
    Assert.False(group.HasError);
    Assert.Single(group.Items);
    service.StoryResponse = Task.FromResult(new NewsFeedPage([], new(null, false, null)));
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal("1 related article", group.CountLabel);
  }

  private static StoryRelatedArticles Group(int count) =>
      new("story-1", "primary", Enumerable.Range(1, count).Select(index => Item($"peer-{index}")).ToArray(),
          new("opaque+/=", true, null), UiLocalization.English);
  private static NewsFeedItem Primary(StoryRelatedArticles group) => Item("primary") with { StoryId = "story-1", StoryArticles = group };
  private static NewsFeedItem Item(string id) => new(id, id, "Source", "Summary", null, DateTimeOffset.UnixEpoch);

  private sealed class PendingBookmarkService : IBookmarkService
  {
    public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task SetAsync(string entityType, string entityId, BookmarkPredicate predicate, bool active,
        CancellationToken cancellationToken = default) => Completion.Task;
  }

  private sealed class SeparateBookmarkService : IBookmarkService
  {
    public TaskCompletionSource Peer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Primary { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task SetAsync(string entityType, string entityId, BookmarkPredicate predicate, bool active,
        CancellationToken cancellationToken = default) => entityId == "primary" ? Primary.Task : Peer.Task;
  }

  private sealed class Service(NewsFeedPage feed) : INewsFeedService, IStoryRelatedArticlesService
  {
    public NewsFeedPage Feed { get; set; } = feed;
    public Task<NewsFeedPage>? FeedResponse { get; set; }
    public List<string?> StoryCursors { get; } = [];
    public Task<NewsFeedPage> StoryResponse { get; set; } = Task.FromResult(new NewsFeedPage([], new(null, false, null)));
    public Task<NewsFeedPage> GetStoryRelatedArticlesPageAsync(string storyId, string primaryItemId, string? after, CancellationToken cancellationToken = default)
    {
      Assert.Equal("story-1", storyId);
      Assert.Equal("primary", primaryItemId);
      StoryCursors.Add(after);
      return StoryResponse;
    }
    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType, string? after = null, int limit = 20, CancellationToken cancellationToken = default) => FeedResponse ?? Task.FromResult(Feed);
    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, string? after = null, int limit = 20, CancellationToken cancellationToken = default) => FeedResponse ?? Task.FromResult(Feed);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, CancellationToken cancellationToken = default) => Task.FromResult(Feed.Items);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType, CancellationToken cancellationToken = default) => Task.FromResult(Feed.Items);
    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

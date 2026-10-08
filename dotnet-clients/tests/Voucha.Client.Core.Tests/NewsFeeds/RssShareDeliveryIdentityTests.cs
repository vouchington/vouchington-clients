using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class RssShareDeliveryIdentityTests
{
  [Fact]
  public async Task FailedStoryPeerHideRestoresInterleavedShareDeliveries()
  {
    var peer = Delivery("article", "preview") with { DeliveryId = null };
    var group = new StoryRelatedArticles("story-1", "primary", [peer], new(null, false, null), UiLocalization.English);
    var primary = Delivery("primary", "direct") with { StoryId = "story-1", StoryArticles = group };
    var bookmarks = new HeldBookmarkService();
    var service = new Pages(new NewsFeedPage([primary, Delivery("article", "share-A"), Delivery("other", "other"),
        Delivery("article", "share-B")], new(null, false, null)));
    var model = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? hide = null;
    try
    {
      await model.LoadAsync(cancellation.Token);
      hide = model.ToggleHideAsync(model.Items[1], cancellation.Token);
      await bookmarks.RequestStarted.Task.WaitAsync(cancellation.Token);
      Assert.Equal(["direct", "other"], model.Items.Select(item => item.FeedRowId));
      Assert.Empty(group.Items);

      bookmarks.FailHide(new InvalidOperationException("Hide failed."));
      await hide;
      Assert.Equal(["direct", "share-A", "other", "share-B"], model.Items.Select(item => item.FeedRowId));
      Assert.Equal("article", Assert.Single(group.Items).Id);
      Assert.Equal("Hide failed.", model.ErrorMessage);
    }
    finally
    {
      cancellation.Cancel();
      bookmarks.FailHide(new OperationCanceledException("Test cleanup."));
      if (hide is not null) await hide;
    }
  }

  [Fact]
  public async Task StoryPeerMutationsKeepEveryShareDeliveryIdentity()
  {
    var peer = Delivery("article", "preview") with { DeliveryId = null };
    var group = new StoryRelatedArticles("story-1", "primary", [peer], new(null, false, null), UiLocalization.English);
    var primary = Delivery("primary", "direct") with { StoryId = "story-1", StoryArticles = group };
    var bookmarks = new RecordingBookmarks();
    var service = new Pages(
        new([primary, Delivery("article", "share-A"), Delivery("article", "share-B")], new("next", true, null)),
        new([], new(null, false, null)));
    var model = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarks);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.ToggleSaveAsync(model.Items[1], TestContext.Current.CancellationToken);
    Assert.Single(bookmarks.Requests);
    Assert.True(group.Items[0].IsSaved);
    Assert.All(model.Items.Skip(1), item => Assert.True(item.IsSaved));
    Assert.Equal(["direct", "share-A", "share-B"], model.Items.Select(item => item.FeedRowId));

    await model.ToggleSaveAsync(group.Items[0], TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, bookmarks.Requests.Count);
    Assert.False(group.Items[0].IsSaved);
    Assert.Equal(["direct", "share-A", "share-B"], model.Items.Select(item => item.FeedRowId));
    Assert.All(model.Items.Skip(1), item => Assert.False(item.IsSaved));
  }

  [Fact]
  public async Task FeedPagesKeepEachDeliveryAndShareItemHydration()
  {
    var direct = Delivery("item-X", "direct") with { StoryId = "story-1" };
    var service = new Pages(
        new([direct, Delivery("item-X", "share-A"), Delivery("item-X", "share-B")], new("c2", true, null)),
        new(
            [Delivery("item-X", "direct"), Delivery("item-X", "share-C")],
            new(null, false, null)));
    var model = new NewsFeedsViewModel(service);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["direct", "share-A", "share-B", "share-C"], model.Items.Select(item => item.FeedRowId));
    Assert.Equal(["item-X", "item-X", "item-X", "item-X"], model.Items.Select(item => item.Id));
    Assert.All(model.Items, item => Assert.Equal("Shared article", item.Title));
    Assert.Equal("story-1", model.Items[0].StoryId);
    Assert.All(model.Items.Skip(1), item => Assert.Null(item.StoryId));
  }

  private static NewsFeedItem Delivery(string itemId, string deliveryId) =>
      new(itemId, "Shared article", "Source", "Summary", null, DateTimeOffset.UnixEpoch, DeliveryId: deliveryId);

  private sealed class RecordingBookmarks : IBookmarkService
  {
    public List<(string EntityId, BookmarkPredicate Predicate, bool Active)> Requests { get; } = [];

    public Task SetAsync(string entityType, string entityId, BookmarkPredicate predicate, bool active,
        CancellationToken cancellationToken = default)
    {
      Requests.Add((entityId, predicate, active));
      return Task.CompletedTask;
    }
  }

  private sealed class Pages(params NewsFeedPage[] pages) : INewsFeedService
  {
    private readonly Queue<NewsFeedPage> remaining = new(pages);

    public Task<NewsFeedPage> GetNewsFeedPageAsync(
        NewsFeedScope scope,
        string? after = null,
        int limit = 20,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(remaining.Count == 0 ? new NewsFeedPage([], new(null, false, null)) : remaining.Dequeue());

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);

    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }
}

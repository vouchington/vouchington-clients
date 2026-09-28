using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class RssShareDeliveryIdentityTests
{
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

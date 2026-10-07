using Voucha.Client.Core.Api;
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
    Assert.Single(group.Items);
    Assert.True(group.IsExpanded);
    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal(["opaque+/="], service.StoryCursors);
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

  private sealed class Service(NewsFeedPage feed) : INewsFeedService, IStoryRelatedArticlesService
  {
    public NewsFeedPage Feed { get; set; } = feed;
    public List<string?> StoryCursors { get; } = [];
    public Task<NewsFeedPage> StoryResponse { get; set; } = Task.FromResult(new NewsFeedPage([], new(null, false, null)));
    public Task<NewsFeedPage> GetStoryRelatedArticlesPageAsync(string storyId, string primaryItemId, string? after, CancellationToken cancellationToken = default)
    {
      Assert.Equal("story-1", storyId);
      Assert.Equal("primary", primaryItemId);
      StoryCursors.Add(after);
      return StoryResponse;
    }
    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType, string? after = null, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult(Feed);
    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, string? after = null, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult(Feed);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, CancellationToken cancellationToken = default) => Task.FromResult(Feed.Items);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType, CancellationToken cancellationToken = default) => Task.FromResult(Feed.Items);
    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

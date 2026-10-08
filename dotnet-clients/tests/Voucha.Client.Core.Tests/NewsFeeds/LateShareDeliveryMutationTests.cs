using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class LateShareDeliveryMutationTests
{
  [Fact]
  public async Task LateStoryPreviewPromotesOnlyTheDirectDelivery()
  {
    var direct = Delivery("article", "direct") with { StoryId = "story" };
    var group = new StoryRelatedArticles("story", "later", [], new(null, false, null), UiLocalization.English);
    var service = new HeldService([direct, Delivery("article", "share")]);
    using var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var continuation = model.LoadMoreAsync(TestContext.Current.CancellationToken);
    service.PageResult.TrySetResult(new([Delivery("later", "later") with { StoryId = "story", StoryArticles = group }], new(null, false, null)));
    await continuation;

    Assert.Equal(["direct", "share"], model.Items.Select(item => item.FeedRowId));
    Assert.NotNull(model.Items[0].StoryArticles);
    Assert.Equal("article", model.Items[0].StoryArticles!.PrimaryItemId);
    Assert.Null(model.Items[1].StoryId);
    Assert.Null(model.Items[1].StoryArticles);
    Assert.False(model.Items[1].CanStartStoryDiscussion);
  }

  [Theory]
  [InlineData("save", false)]
  [InlineData("read", false)]
  [InlineData("vote", false)]
  [InlineData("save", true)]
  [InlineData("read", true)]
  [InlineData("vote", true)]
  public async Task SuccessfulMutationUpdatesDeliveryAppendedWhilePending(string action, bool throughStoryPeer)
  {
    var target = Delivery("article", "share-A");
    var group = new StoryRelatedArticles("story", "primary", [target with { DeliveryId = null }], new(null, false, null), UiLocalization.English);
    var initial = throughStoryPeer
        ? new[] { Delivery("primary", "primary") with { StoryId = "story", StoryArticles = group }, target }
        : new[] { target };
    var service = new HeldService(initial);
    using var model = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, service);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? continuation = null;
    Task? mutation = null;
    try
    {
      await model.LoadAsync(cancellation.Token);
      continuation = model.LoadMoreAsync(cancellation.Token);
      await service.PageStarted.Task.WaitAsync(cancellation.Token);
      var displayed = model.Items.First(item => item.Id == "article");
      mutation = Mutate(model, displayed, action, cancellation.Token);
      await service.MutationStarted.Task.WaitAsync(cancellation.Token);
      service.PageResult.TrySetResult(new([Delivery("article", "share-B"), Delivery("other", "other")], new(null, false, null)));
      await continuation;
      Assert.Equal(2, model.Items.Count(item => item.Id == "article"));
      service.MutationResult.TrySetResult();
      await mutation;

      Assert.Equal(["share-A", "share-B"], model.Items.Where(item => item.Id == "article").Select(item => item.FeedRowId));
      Assert.All(model.Items.Where(item => item.Id == "article"), item => AssertMutation(item, action));
      Assert.Contains(model.Items, item => item.FeedRowId == "other");
      if (throughStoryPeer) AssertMutation(Assert.Single(group.Items), action);
      Assert.False(model.HasMore);
    }
    finally
    {
      cancellation.Cancel();
      service.PageResult.TrySetCanceled(cancellation.Token);
      service.MutationResult.TrySetCanceled(cancellation.Token);
      if (continuation is not null) await continuation;
      if (mutation is not null) await mutation;
    }
  }

  [Theory]
  [InlineData("save", false)]
  [InlineData("read", false)]
  [InlineData("vote", false)]
  [InlineData("save", true)]
  [InlineData("read", true)]
  [InlineData("vote", true)]
  public async Task SuccessfulOldMutationDoesNotProjectIntoReloadedFeed(string action, bool throughStoryPeer)
  {
    var target = Delivery("article", "old");
    var group = new StoryRelatedArticles("story", "primary", [target with { DeliveryId = null }], new(null, false, null), UiLocalization.English);
    var service = new HeldService(throughStoryPeer
        ? [Delivery("primary", "primary") with { StoryId = "story", StoryArticles = group }, target]
        : [target]);
    using var model = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, service);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? mutation = null;
    try
    {
      await model.LoadAsync(cancellation.Token);
      mutation = Mutate(model, model.Items.First(item => item.Id == "article"), action, cancellation.Token);
      await service.MutationStarted.Task.WaitAsync(cancellation.Token);
      service.Initial = [Delivery("article", "replacement")];
      await model.LoadAsync(cancellation.Token);
      service.MutationResult.TrySetResult();
      await mutation;

      var replacement = Assert.Single(model.Items);
      Assert.Equal("replacement", replacement.FeedRowId);
      Assert.False(replacement.IsSaved);
      Assert.False(replacement.IsRead);
      Assert.Equal(ElectionVoteChoice.Like, replacement.CurrentVoteChoice);
      Assert.Equal(4, replacement.VoteCountUp);
      Assert.Equal(1, replacement.VoteCountDown);
    }
    finally
    {
      cancellation.Cancel();
      service.MutationResult.TrySetCanceled(cancellation.Token);
      if (mutation is not null) await mutation;
    }
  }

  private static Task Mutate(NewsFeedsViewModel model, NewsFeedItem item, string action, CancellationToken cancellationToken) => action switch
  {
    "save" => model.ToggleSaveAsync(item, cancellationToken),
    "read" => model.ToggleReadAsync(item, cancellationToken),
    _ => model.VoteRssFeedItemAsync(item, ElectionVoteChoice.Dislike, cancellationToken),
  };

  private static void AssertMutation(NewsFeedItem item, string action)
  {
    if (action == "save") Assert.True(item.IsSaved);
    else if (action == "read") Assert.True(item.IsRead);
    else
    {
      Assert.Equal(ElectionVoteChoice.Dislike, item.CurrentVoteChoice);
      Assert.Equal(3, item.VoteCountUp);
      Assert.Equal(2, item.VoteCountDown);
    }
  }

  private static NewsFeedItem Delivery(string id, string delivery) =>
      new(id, id, "Source", "Summary", null, DateTimeOffset.UnixEpoch, VoteCountUp: 4, VoteCountDown: 1,
          CurrentVoteChoice: ElectionVoteChoice.Like, DeliveryId: delivery);

  private sealed class HeldService(NewsFeedItem[] initial) : INewsFeedService, IBookmarkService
  {
    public NewsFeedItem[] Initial { get; set; } = initial;
    public TaskCompletionSource PageStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<NewsFeedPage> PageResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource MutationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource MutationResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, string? after = null, int limit = 20,
        CancellationToken cancellationToken = default)
    {
      if (after is null) return Task.FromResult(new NewsFeedPage(Initial, new("next", true, null)));
      PageStarted.TrySetResult();
      return PageResult.Task.WaitAsync(cancellationToken);
    }

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);
    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) => Mutate(cancellationToken);
    public Task VoteRssFeedItemAsync(string itemId, ElectionVoteChoice choice, CancellationToken cancellationToken = default) => Mutate(cancellationToken);
    public Task SetAsync(string entityType, string entityId, BookmarkPredicate predicate, bool active,
        CancellationToken cancellationToken = default) => Mutate(cancellationToken);

    private Task Mutate(CancellationToken cancellationToken)
    {
      MutationStarted.TrySetResult();
      return MutationResult.Task.WaitAsync(cancellationToken);
    }
  }
}

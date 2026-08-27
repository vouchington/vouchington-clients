using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelBookmarkTests
{
  [Fact]
  public async Task ToggleSaveAndHideAsyncUseBookmarkServiceAndUpdateState()
  {
    var bookmarkService = new RecordingBookmarkService();
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "article-1",
              "Article",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              IsSaved: false,
              IsHidden: false),
          new NewsFeedItem(
              "media-1",
              "Episode",
              "Podcast",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Media,
              IsSaved: false,
              IsHidden: false),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarkService);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleSaveAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    Assert.True(viewModel.Items[0].IsSaved);
    await viewModel.ToggleSaveAsync(viewModel.Items[1], TestContext.Current.CancellationToken);
    Assert.True(viewModel.Items[1].IsSaved);
    await viewModel.ToggleHideAsync(viewModel.Items[1], TestContext.Current.CancellationToken);
    await viewModel.ToggleHideAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.Equal([
      ("rss_feed_item", "article-1", BookmarkPredicate.Save, true),
      ("rss_feed_item", "media-1", BookmarkPredicate.Save, true),
      ("rss_feed_item", "media-1", BookmarkPredicate.Hide, true),
      ("rss_feed_item", "article-1", BookmarkPredicate.Hide, true),
    ], bookmarkService.Calls);
  }

  [Fact]
  public async Task ToggleSourceAndTopicMuteAsyncUseBookmarkService()
  {
    var bookmarkService = new RecordingBookmarkService();
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "source-1",
              "Source",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source,
              TopicId: "topic-1"),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources, bookmarkService);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleSourceMuteAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await viewModel.ToggleTopicMuteAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsMutedSource);
    Assert.True(viewModel.Items[0].IsMutedTopic);
    Assert.Equal([
      ("rss_feed", "source-1", BookmarkPredicate.Mute, true),
      ("topic", "topic-1", BookmarkPredicate.Mute, true),
    ], bookmarkService.Calls);
  }

  [Fact]
  public async Task ToggleSaveAsyncRollsBackAndSetsErrorWhenBookmarkFails()
  {
    var bookmarkService = new RecordingBookmarkService { Fail = true };
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "article-1",
              "Article",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarkService);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleSaveAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.False(viewModel.Items[0].IsSaved);
    Assert.True(viewModel.HasError);
    Assert.Equal("Bookmark failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleHideAsyncRestoresItemsWithoutErrorWhenCancelled()
  {
    var bookmarkService = new RecordingBookmarkService { Cancel = true };
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "article-1",
              "Article",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarkService);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleHideAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Collection(viewModel.Items, item => Assert.Equal("article-1", item.Id));
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task ToggleSourceMuteAsyncRemovesYourSourceAndRollsBackOnFailure()
  {
    var bookmarkService = new RecordingBookmarkService { Fail = true };
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "source-1",
              "Source",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source,
              IsFollowingSource: true),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.YourSources, bookmarkService);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleSourceMuteAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Collection(viewModel.Items, item => Assert.Equal("source-1", item.Id));
    Assert.True(viewModel.Items[0].IsFollowingSource);
    Assert.False(viewModel.Items[0].IsMutedSource);
    Assert.Equal("Bookmark failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleSourceMuteAsyncRemovesMediaSourceFromYourSources()
  {
    var bookmarkService = new RecordingBookmarkService();
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "source-1",
              "Podcast",
              "Show",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source,
              IsFollowingSource: true),
        ]);
    var viewModel = new NewsFeedsViewModel(
        service,
        NewsFeedKind.Podcasts,
        NewsFeedScope.YourPodcastSources,
        bookmarkService);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleSourceMuteAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.Equal([("rss_feed", "source-1", BookmarkPredicate.Mute, true)], bookmarkService.Calls);
  }

  [Fact]
  public async Task ToggleTopicMuteAsyncUpdatesMatchingSourcesAndDisablesFollow()
  {
    var bookmarkService = new RecordingBookmarkService();
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "source-1",
              "Source",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source,
              TopicId: "topic-1",
              IsFollowingTopic: true),
          new NewsFeedItem(
              "source-2",
              "Other source",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source,
              TopicId: "topic-2",
              IsFollowingTopic: true),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources, bookmarkService);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleTopicMuteAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsMutedTopic);
    Assert.False(viewModel.Items[0].IsFollowingTopic);
    Assert.False(viewModel.Items[1].IsMutedTopic);
    Assert.True(viewModel.Items[1].IsFollowingTopic);
  }

  [Fact]
  public async Task BookmarkTogglesIgnoreUnsupportedItemKinds()
  {
    var bookmarkService = new RecordingBookmarkService();
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "source-1",
              "Source",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source),
          new NewsFeedItem(
              "article-1",
              "Article",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews, bookmarkService);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleSaveAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await viewModel.ToggleHideAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await viewModel.ToggleSourceMuteAsync(viewModel.Items[1], TestContext.Current.CancellationToken);
    await viewModel.ToggleTopicMuteAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Empty(bookmarkService.Calls);
  }

  private sealed class RecordingNewsFeedService(IReadOnlyList<NewsFeedItem> items) : INewsFeedService
  {
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(items);

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default) =>
        GetNewsFeedItemsAsync(scope, cancellationToken);

    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }

  private sealed class RecordingBookmarkService : IBookmarkService
  {
    public List<(string EntityType, string EntityId, BookmarkPredicate Predicate, bool Active)> Calls { get; } = [];

    public bool Fail { get; init; }

    public bool Cancel { get; init; }

    public Task SetAsync(
        string entityType,
        string entityId,
        BookmarkPredicate predicate,
        bool active,
        CancellationToken cancellationToken = default)
    {
      Calls.Add((entityType, entityId, predicate, active));
      if (Cancel)
      {
        return Task.FromCanceled(cancellationToken.IsCancellationRequested
            ? cancellationToken
            : new CancellationToken(canceled: true));
      }
      if (Fail)
      {
        return Task.FromException(new InvalidOperationException("Bookmark failed."));
      }
      return Task.CompletedTask;
    }
  }
}

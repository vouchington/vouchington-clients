using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelProvenanceLocaleTests
{
  [Fact]
  public async Task LocaleChangeReprojectsRowsWithoutReloadingTheFeed()
  {
    using var controller = new UiLocaleController(new DeviceLanguage("en"));
    var localization = new UiLocalization(controller);
    var handler = new RecordingHandler(File.ReadAllText(FilamentsContractPaths.ApiFixture(
        "responses/entity-provenance.rss-feeds.json")));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    using var viewModel = new NewsFeedsViewModel(
        new ApiNewsFeedService(client),
        NewsFeedKind.News,
        NewsFeedScope.AllSources,
        localization: localization,
        localeController: controller);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var originalItems = viewModel.Items;
    var original = Assert.Single(originalItems, item => item.Provenance?.App?.ClientName == "Fixture Agent");
    var englishLabel = original.LocalizedProvenanceLabel;

    controller.ApplySavedLocale("es");

    var refreshedItems = viewModel.Items;
    var refreshed = Assert.Single(refreshedItems, item => item.Provenance?.App?.ClientName == "Fixture Agent");
    Assert.NotSame(originalItems, refreshedItems);
    Assert.NotSame(original, refreshed);
    Assert.NotEqual(englishLabel, refreshed.LocalizedProvenanceLabel);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task LocaleChangeDuringReplacementDoesNotRestoreRowsIntoTheResetPage()
  {
    using var controller = new UiLocaleController(new DeviceLanguage("en"));
    var localization = new UiLocalization(controller);
    var oldItem = new NewsFeedItem(
        "deleted-article",
        "Deleted article",
        "Fixture source",
        "Summary",
        null,
        DateTimeOffset.UnixEpoch,
        Provenance: new("mcp", new("verified", ClientName: "Old Agent")));
    var newItem = new NewsFeedItem(
        "current-article",
        "Current article",
        "Fixture source",
        "Summary",
        null,
        DateTimeOffset.UnixEpoch);
    var feed = new HeldContinuationFeedService(oldItem);
    using var viewModel = new NewsFeedsViewModel(
        feed,
        NewsFeedKind.News,
        NewsFeedScope.AllNews,
        localization: localization,
        localeController: controller);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? refresh = null;
    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      refresh = viewModel.LoadAsync(cancellation.Token);
      await feed.ContinuationStarted.Task.WaitAsync(cancellation.Token);

      controller.ApplySavedLocale("es");
      Assert.Equal("deleted-article", Assert.Single(viewModel.Items).Id);

      feed.ReleaseContinuation(new NewsFeedPage([newItem], new("cursor-2", false, null)));
      await refresh;

      Assert.Equal("current-article", Assert.Single(viewModel.Items).Id);
    }
    finally
    {
      cancellation.Cancel();
      feed.ReleaseContinuation(new NewsFeedPage([], new(null, false, null)));
      if (refresh is not null) await refresh;
    }
  }

  [Fact]
  public async Task ConcurrentReadTogglesForTheSameArticleAreIgnoredWhileMutationIsPending()
  {
    var item = new NewsFeedItem(
        "article-1",
        "Article",
        "Fixture source",
        "Summary",
        null,
        DateTimeOffset.UnixEpoch);
    var feed = new HeldContinuationFeedService(item);
    var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var readResult = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
    var readCalls = 0;
    feed.ReadHandler = async (_, _, cancellationToken) =>
    {
      if (Interlocked.Increment(ref readCalls) > 1) return;
      readStarted.TrySetResult();
      var error = await readResult.Task.WaitAsync(cancellationToken);
      if (error is not null) throw error;
    };
    using var viewModel = new NewsFeedsViewModel(feed, NewsFeedScope.AllNews);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? mutation = null;
    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      var staleItem = Assert.Single(viewModel.Items);
      mutation = viewModel.ToggleReadAsync(staleItem, cancellation.Token);
      await readStarted.Task.WaitAsync(cancellation.Token);

      await viewModel.ToggleReadAsync(staleItem, cancellation.Token);
      Assert.Equal(1, Volatile.Read(ref readCalls));
      Assert.True(Assert.Single(viewModel.Items).IsRead);

      readResult.TrySetResult(new InvalidOperationException("Read failed."));
      await mutation;

      Assert.False(Assert.Single(viewModel.Items).IsRead);
      Assert.Equal("Read failed.", viewModel.ErrorMessage);

      await viewModel.ToggleReadAsync(viewModel.Items[0], cancellation.Token);
      Assert.Equal(2, Volatile.Read(ref readCalls));
      Assert.True(Assert.Single(viewModel.Items).IsRead);
    }
    finally
    {
      cancellation.Cancel();
      readResult.TrySetResult(new OperationCanceledException("Test cleanup."));
      if (mutation is not null) await mutation;
    }
  }

  [Fact]
  public async Task FailedSaveAfterLocaleChangeRollsBackTheOptimisticState()
  {
    using var controller = new UiLocaleController(new DeviceLanguage("en"));
    var localization = new UiLocalization(controller);
    var item = new NewsFeedItem(
        "article-1",
        "Fixture article",
        "Fixture source",
        "Summary",
        null,
        DateTimeOffset.UnixEpoch,
        Provenance: new("mcp", new("verified", ClientName: "Fixture Agent")));
    var feed = new HeldContinuationFeedService(item);
    var bookmarks = new HeldBookmarkService();
    using var viewModel = new NewsFeedsViewModel(
        feed,
        NewsFeedKind.News,
        NewsFeedScope.AllNews,
        bookmarks,
        localization: localization,
        localeController: controller);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? mutation = null;
    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      var englishLabel = Assert.Single(viewModel.Items).LocalizedProvenanceLabel;
      mutation = viewModel.ToggleSaveAsync(viewModel.Items[0], cancellation.Token);
      await bookmarks.RequestStarted.Task.WaitAsync(cancellation.Token);

      Assert.True(Assert.Single(viewModel.Items).IsSaved);
      controller.ApplySavedLocale("es");
      bookmarks.FailHide(new InvalidOperationException("Hide failed."));
      await mutation;

      var restored = Assert.Single(viewModel.Items);
      Assert.False(restored.IsSaved);
      Assert.NotEqual(englishLabel, restored.LocalizedProvenanceLabel);
      Assert.Equal("Hide failed.", viewModel.ErrorMessage);
    }
    finally
    {
      cancellation.Cancel();
      bookmarks.FailHide(new OperationCanceledException("Test cleanup."));
      if (mutation is not null) await mutation;
    }
  }

  [Fact]
  public async Task FailedSaveDoesNotRollBackOverANewerItemMutation()
  {
    var first = new NewsFeedItem("article-1", "First", "Fixture", "", null, DateTimeOffset.UnixEpoch);
    var second = new NewsFeedItem("article-2", "Second", "Fixture", "", null, DateTimeOffset.UnixEpoch);
    var bookmarks = new ConcurrentBookmarkService(expectedRequests: 2);
    using var viewModel = new NewsFeedsViewModel(
        new HeldContinuationFeedService(first, second),
        NewsFeedKind.News,
        NewsFeedScope.AllNews,
        bookmarks);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    Task? firstMutation = null;
    Task? secondMutation = null;
    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      firstMutation = viewModel.ToggleSaveAsync(viewModel.Items[0], cancellation.Token);
      secondMutation = viewModel.ToggleSaveAsync(viewModel.Items[1], cancellation.Token);
      await bookmarks.RequestsStarted.Task.WaitAsync(cancellation.Token);

      bookmarks.Complete("article-1", new InvalidOperationException("First save failed."));
      await firstMutation;

      Assert.True(Assert.Single(viewModel.Items, item => item.Id == "article-2").IsSaved);

      bookmarks.Complete("article-2");
      await secondMutation;

      Assert.True(Assert.Single(viewModel.Items, item => item.Id == "article-2").IsSaved);
    }
    finally
    {
      cancellation.Cancel();
      bookmarks.Complete("article-1", new OperationCanceledException("Test cleanup."));
      bookmarks.Complete("article-2", new OperationCanceledException("Test cleanup."));
      if (firstMutation is not null) await firstMutation;
      if (secondMutation is not null) await secondMutation;
    }
  }

  private sealed class DeviceLanguage(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }

  private sealed class ConcurrentBookmarkService(int expectedRequests) : IBookmarkService
  {
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<Exception?>> requests = new(StringComparer.Ordinal);
    private int requestCount;

    public TaskCompletionSource RequestsStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Complete(string itemId, Exception? error = null) => Request(itemId).TrySetResult(error);

    public async Task SetAsync(
        string entityType,
        string entityId,
        BookmarkPredicate predicate,
        bool active,
        CancellationToken cancellationToken = default)
    {
      if (Interlocked.Increment(ref requestCount) == expectedRequests) RequestsStarted.TrySetResult();
      var error = await Request(entityId).Task.WaitAsync(cancellationToken);
      if (error is not null) throw error;
    }

    private TaskCompletionSource<Exception?> Request(string itemId) =>
        requests.GetOrAdd(itemId, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
  }
}

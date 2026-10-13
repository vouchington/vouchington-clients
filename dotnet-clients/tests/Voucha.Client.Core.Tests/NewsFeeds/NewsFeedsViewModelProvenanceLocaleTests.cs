using Voucha.Client.Core.Api;
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
    Assert.NotSame(original, refreshed);
    Assert.NotEqual(englishLabel, refreshed.LocalizedProvenanceLabel);
    Assert.Single(handler.Requests);
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

  private sealed class DeviceLanguage(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}

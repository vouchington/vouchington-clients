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
    Assert.NotSame(originalItems, refreshedItems);
    Assert.NotSame(original, refreshed);
    Assert.NotEqual(englishLabel, refreshed.LocalizedProvenanceLabel);
    Assert.Single(handler.Requests);
  }

  private sealed class DeviceLanguage(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}

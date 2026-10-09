using Voucha.Client.Core.Api;
using Voucha.Client.Core.Lists;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Lists;

public sealed class ListsViewModelProvenanceLocaleTests
{
  [Fact]
  public async Task LocaleChangeRefreshesSelectedProvenanceWithoutDiscardingDraftEdits()
  {
    using var controller = new UiLocaleController(new DeviceLanguage("en"));
    var localization = new UiLocalization(controller);
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("entity-provenance.lists")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.list-items.default")),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    using var viewModel = new ListsViewModel(client, localization, controller);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var englishLabel = viewModel.SelectedList?.LocalizedProvenanceLabel;
    viewModel.SelectedListName = "Unsaved draft title";
    viewModel.SelectedListDescription = "Unsaved draft description";

    controller.ApplySavedLocale("es");

    Assert.NotNull(englishLabel);
    Assert.NotEqual(englishLabel, viewModel.SelectedList?.LocalizedProvenanceLabel);
    Assert.Equal("Unsaved draft title", viewModel.SelectedListName);
    Assert.Equal("Unsaved draft description", viewModel.SelectedListDescription);
    Assert.Equal(2, handler.Requests.Count);
  }

  private sealed class DeviceLanguage(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}

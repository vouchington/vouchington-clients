using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  [Theory]
  [InlineData("en", "Topics")]
  [InlineData("es", "Temas")]
  [InlineData("fr", "Sujets")]
  [InlineData("pt", "Tópicos")]
  public async Task ProductionLocalizedConstructorPathSupportsEveryLocale(
      string locale,
      string expectedTitle)
  {
    var handler = new RecordingHandler(CombinedSearchJson);
    var client = new VouchaApiClient(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var controller = new UiLocaleController(new StubLanguageProvider(locale));
    using var viewModel = new OmnisearchViewModel(
        client,
        localization: new UiLocalization(controller),
        localeController: controller)
    {
      Query = "native",
    };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expectedTitle, viewModel.Groups[0].Title);
  }

  private sealed class StubLanguageProvider(string locale) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [locale];
  }
}

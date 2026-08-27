using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class SupportThreadsLocalizationTests
{
  [Theory]
  [InlineData("en", "open", "Open")]
  [InlineData("en", "closed", "Closed")]
  [InlineData("es", "open", "Abierto")]
  [InlineData("es", "closed", "Cerrado")]
  [InlineData("fr", "open", "Ouvert")]
  [InlineData("fr", "closed", "Fermé")]
  [InlineData("pt", "open", "Aberto")]
  [InlineData("pt", "closed", "Fechado")]
  public async Task RowsLocalizeSupportStatusWithoutLosingProtocolValue(
      string locale,
      string protocolStatus,
      string expectedStatus)
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider(locale));
    var service = new FakeChatService
    {
      SupportThreadsResult = Response(protocolStatus),
    };
    using var viewModel = new SupportThreadsViewModel(
        service,
        new UiLocalization(controller),
        controller);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Threads);
    Assert.Equal(protocolStatus, row.ProtocolStatus);
    Assert.Equal(expectedStatus, row.LocalizedStatus);
  }

  [Fact]
  public async Task VisibleSupportStatusRefreshesWhenLocaleChanges()
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var viewModel = new SupportThreadsViewModel(
        new FakeChatService { SupportThreadsResult = Response("open") },
        new UiLocalization(controller),
        controller);
    var refreshed = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(SupportThreadsViewModel.Threads)) refreshed++;
    };
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var refreshesBeforeLocaleChange = refreshed;

    controller.ApplySavedLocale("es");

    Assert.Equal("Abierto", Assert.Single(viewModel.Threads).LocalizedStatus);
    Assert.Equal(refreshesBeforeLocaleChange + 1, refreshed);
  }

  private static SupportThreadListResponse Response(string status) =>
      new(
          [
            new SupportThread(
                "thread-1",
                "contact-1",
                "Need help",
                null,
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                DateTimeOffset.Parse("2026-07-01T10:05:00Z"),
                null,
                null,
                null,
                null,
                Enum.Parse<SupportThreadStatus>(status, ignoreCase: true)),
          ],
          new PageInfo(null, false, null));

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}

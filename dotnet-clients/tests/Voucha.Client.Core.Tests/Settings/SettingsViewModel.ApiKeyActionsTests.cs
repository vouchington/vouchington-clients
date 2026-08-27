using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task CreateApiKeyAsyncSetsTheRawSecretAndClearsTheLabel()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };

    await viewModel.CreateApiKeyAsync(TestContext.Current.CancellationToken);

    Assert.Equal("raw-key", viewModel.ApiKeySecret);
    Assert.Equal("Created key ****. Copy it now; it will not be shown again.", viewModel.ApiKeySecretDisplay);
    Assert.True(viewModel.HasApiKeySecret);
    Assert.Equal(string.Empty, viewModel.ApiKeyLabel);
    Assert.Equal(1, service.FetchApiKeysCount);
    Assert.Equal("Reader", service.LastCreatedApiKeyLabel);
    Assert.Equal("mcp", service.LastCreatedApiKeyType);
  }

  [Fact]
  public async Task RevokeApiKeyAsyncDeletesTheApiKeyWithoutDiscardingPaginationState()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    await viewModel.RevokeApiKeyAsync(
        new ApiKey("api-key-1", "user-1", "rk_abc123", "rss", "Reader", ["rss-feeds:read"],
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"), null, null,
            DateTimeOffset.Parse("2026-07-01T12:00:00Z")),
        TestContext.Current.CancellationToken);

    Assert.Equal(0, service.FetchApiKeysCount);
    Assert.Equal("api-key-1", service.LastDeletedApiKeyId);
  }

  [Fact]
  public async Task CreatedApiKeyNoticeRefreshesWhenTheLocaleChanges()
  {
    var service = new RecordingSettingsService();
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    using var viewModel = new SettingsViewModel(
        service,
        uiLocaleController: controller,
        localization: new UiLocalization(controller))
    {
      ApiKeyLabel = "Reader",
      ApiKeyType = "mcp",
    };

    await viewModel.CreateApiKeyAsync(TestContext.Current.CancellationToken);
    controller.ApplySavedLocale("fr");

    Assert.Equal(
        "Clé **** créée. Copiez-la maintenant; elle ne sera plus affichée.",
        viewModel.ApiKeySecretDisplay);
  }

  private sealed class EnglishDeviceLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }
}

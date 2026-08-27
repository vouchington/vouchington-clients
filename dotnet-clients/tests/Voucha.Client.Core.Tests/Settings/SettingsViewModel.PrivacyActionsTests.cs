using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task UpdatePrivacyToggleAsyncUsesTheBooleanPatchField()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    await viewModel.UpdatePrivacyToggleAsync(
        new SettingsToggleRowViewModel(
            "third_party_marketing",
            UiText.Verbatim("Third-party marketing"),
            UiText.Verbatim("desc"),
            true,
            UiLocalization.English),
        TestContext.Current.CancellationToken);

    Assert.Equal(2, service.FetchMyIdentityCount);
    Assert.NotNull(service.LastPrivacyUpdateBody);
    Assert.True(service.LastPrivacyUpdateBody!.ThirdPartyMarketing);
    Assert.Null(service.LastPrivacyUpdateBody.ProcessingRestrictedAt);
  }

  [Fact]
  public async Task UpdateHnDiscussionsToggleUsesTheBooleanPatchField()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    await viewModel.UpdatePrivacyToggleAsync(
        new SettingsToggleRowViewModel(
            "hn_discussions",
            UiText.Verbatim("Hacker News discussions"),
            UiText.Verbatim("desc"),
            true,
            UiLocalization.English),
        TestContext.Current.CancellationToken);

    Assert.NotNull(service.LastPrivacyUpdateBody);
    Assert.True(service.LastPrivacyUpdateBody!.HnDiscussions);
    Assert.Null(service.LastPrivacyUpdateBody.ThirdPartyMarketing);
  }
}

using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task LocaleChangeRefreshesMembershipPresentationCopy()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var viewModel = new SettingsViewModel(
        new RecordingSettingsService(),
        uiLocaleController: controller,
        localization: new UiLocalization(controller));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Native purchase and billing management are not available yet.", viewModel.MembershipActionNotice);
    var freePlan = viewModel.MembershipPlanOptions.Single(option => option.Slug == "free");
    Assert.Equal(UiMessageKey.NativeDotnetSettingsPlanFree, freePlan.NameText.Key);
    Assert.Equal(UiMessageKey.NativeDotnetResidualAvailable, freePlan.StateText.Key);
    controller.ApplySavedLocale("fr");

    Assert.Equal("Les achats et la gestion de facturation natifs ne sont pas encore disponibles.", viewModel.MembershipActionNotice);
    Assert.Equal("Gratuit", viewModel.MembershipPlanOptions.Single(option => option.Slug == "free").Name);
  }

  [Fact]
  public async Task UpdatePrivacySelectionAsyncUsesTheLocalePatchField()
  {
    var service = new RecordingSettingsService();
    var localeController = new UiLocaleController(new StubDeviceLanguageProvider("fr-FR"));
    var viewModel = new SettingsViewModel(service, uiLocaleController: localeController);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = viewModel.PrivacySelections.Single(item => item.Key == "ui_locale");
    row.Value = "pt";

    await viewModel.UpdatePrivacySelectionAsync(row, TestContext.Current.CancellationToken);

    Assert.Equal(2, service.FetchMyIdentityCount);
    Assert.NotNull(service.LastPrivacyUpdateBody);
    Assert.Equal("pt", service.LastPrivacyUpdateBody!.UiLocale?.Value);
    Assert.Null(service.LastPrivacyUpdateBody.FollowsVisibility);
    Assert.Equal("pt", localeController.EffectiveLocale);
  }

  [Fact]
  public async Task LoadAsyncUsesSiteDefaultLocaleWhenServerLocaleIsNull()
  {
    var service = new RecordingSettingsService { UserUiLocale = null };
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = viewModel.PrivacySelections.Single(item => item.Key == "ui_locale");
    Assert.Equal(SettingsOptionSets.SiteDefaultUiLocale, row.Value);
    Assert.Contains(
        SettingsOptionSets.SiteDefaultUiLocale,
        row.Options.Select(option => option.Value));
  }

  [Fact]
  public async Task UpdatePrivacySelectionAsyncCanClearLocalePreference()
  {
    var service = new RecordingSettingsService();
    var localeController = new UiLocaleController(new StubDeviceLanguageProvider("fr-FR"));
    localeController.ApplySavedLocale("es");
    var viewModel = new SettingsViewModel(service, uiLocaleController: localeController);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = viewModel.PrivacySelections.Single(item => item.Key == "ui_locale");
    row.Value = SettingsOptionSets.SiteDefaultUiLocale;

    await viewModel.UpdatePrivacySelectionAsync(row, TestContext.Current.CancellationToken);

    Assert.NotNull(service.LastPrivacyUpdateBody);
    Assert.Null(service.LastPrivacyUpdateBody!.UiLocale?.Value);
    Assert.Equal("fr", localeController.EffectiveLocale);
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}

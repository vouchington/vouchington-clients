using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Auth;

public sealed partial class FreshAccountConfirmationViewModel
{
  private ConfirmationProviderRow ProviderRow(string provider) => new(provider, UiText.Localized(provider switch
  {
    "apple" => UiMessageKey.NativeDotnetAuthAppleSignIn,
    "google" => UiMessageKey.NativeTaxonomySettingsGoogle,
    "facebook" => UiMessageKey.NativeTaxonomySettingsFacebook,
    "x" => UiMessageKey.NativeTaxonomySettingsX,
    "github" => UiMessageKey.NativeTaxonomySettingsGithub,
    "linkedin" => UiMessageKey.NativeTaxonomySettingsLinkedin,
    "microsoft" => UiMessageKey.NativeTaxonomySettingsMicrosoft,
    _ => UiMessageKey.ExtractedMyMfaReauthDialogReAuthenticate3887afa4,
  }), localization);
}

public sealed record ConfirmationProviderRow(string Provider, UiText LabelText, IUiLocalization Localization)
{
  public string Label => Localization.Resolve(LabelText);
}

public sealed record ConfirmationPasskeyRow(string Id, UiText LabelText, IUiLocalization Localization)
{
  public string Label => Localization.Resolve(LabelText);
}

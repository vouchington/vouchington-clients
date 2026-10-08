using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private MembershipBenefitBullet[] LegacyMembershipBenefitBullets(string plan)
  {
    if (plan is not ("plus" or "pro")) return [];
    return [new MembershipBenefitBullet(
        UiText.Localized(UiMessageKey.ExtractedMembershipsBenefitCatalogAccess0c11c1a5),
        UiText.Localized(UiMessageKey.ExtractedMembershipsBenefitCatalogImmediate0c11c1ba),
        localization)];
  }

  public string SupportedUiLocaleCount =>
      localization.Format(
          UiMessageKey.SettingsLanguageSupportedCount,
          ("count", SettingsOptionSets.UiLocaleOptions.Count - 1));

  public void Dispose()
  {
    localeSubscription?.Dispose();
    if (ownsLocalLLMResponsesClient) localLLMResponsesClient.Dispose();
  }

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(SupportedUiLocaleCount));
    OnPropertyChanged(nameof(ApiKeyTypeOptions));
    OnPropertyChanged(nameof(SelectedApiKeyTypeOption));
    OnPropertyChanged(nameof(ApiKeyLifetimeOptions));
    OnPropertyChanged(nameof(SelectedApiKeyLifetimeOption));
    OnPropertyChanged(nameof(ApiKeyRotationNotice));
    NotifyScopeSelection();
    OnPropertyChanged(nameof(LocalizedOAuthGrants));
    OnPropertyChanged(nameof(CredentialNotice));
    OnPropertyChanged(nameof(OAuthGrantNotice));
    OnPropertyChanged(nameof(DisplayNameSourceOptions));
    OnPropertyChanged(nameof(SelectedDisplayNameSourceOption));
    OnPropertyChanged(nameof(ProfileLinkTypeOptions));
    OnPropertyChanged(nameof(SelectedProfileLinkTypeOption));
    OnPropertyChanged(nameof(LocalizedApiKeys));
    OnPropertyChanged(nameof(LocalizedProfileLinks));
    OnPropertyChanged(nameof(LocalizedLocalLLMEndpoints));
    UpdateDataRequestState();
    MembershipActionNotice = localization.Localize(UiMessageKey.NativeDotnetResidualBillingUnavailable);
    if (ApiKeySecret is { Length: > 0 } secret)
    {
      ApiKeySecretDisplay = localization.Format(
          UiMessageKey.NativeDotnetSettingsApiKeyCreatedNotice,
          ("maskedKey", MaskSecret(secret)));
    }
    if (lastMembershipPlans is not null)
    {
      MembershipPlanOptions = BuildMembershipPlanOptions(
          lastMembershipPlans,
          Membership,
          lastMembershipBenefitCatalog);
    }
    if (loadedUser is not null)
    {
      PrivacySelections = BuildSelections(loadedUser);
      PrivacyToggles = BuildToggles(loadedUser);
    }
    UpdateMembershipSummary();
    ProfileSummary = ProfileMarkdown.Length == 0
        ? localization.Localize(UiMessageKey.NativeDotnetProfileNoProfileBio)
        : localization.Format(
            UiMessageKey.NativeDotnetSettingsCharacterCount,
            ("count", ProfileMarkdown.Length));
  }

  private static string MaskSecret(string secret) =>
      secret.Length <= 8 ? "****" : $"{secret[..4]}...{secret[^4..]}";

  public IReadOnlyList<SettingsProfileLinkRow> LocalizedProfileLinks =>
      ProfileLinks.Select(link =>
          new SettingsProfileLinkRow(
              link,
              UiTaxonomy.ProfileLinkType(link.LinkType),
              localization)).ToArray();

  public IReadOnlyList<SettingsApiKeyRow> LocalizedApiKeys =>
      ApiKeys.Select(apiKey =>
          new SettingsApiKeyRow(
              apiKey,
              UiTaxonomy.ApiKeyType(apiKey.Type),
              localization,
              IsApiKeyAdministrator)).ToArray();

  public IReadOnlyList<SettingsLocalLLMEndpointRow> LocalizedLocalLLMEndpoints =>
      LocalLLMEndpoints.Select(profile =>
          new SettingsLocalLLMEndpointRow(
              profile,
              LocalChatProviderIds.OpenAICompatible(profile.Id) == LocalLLMSelectedProviderId,
              localization)).ToArray();
}

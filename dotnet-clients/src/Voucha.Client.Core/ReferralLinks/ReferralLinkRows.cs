using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ReferralLinks;

public sealed record ReferralLinkRow(
    string Id,
    UiText TitleText,
    Uri? Url,
    UiText DetailText,
    string? ReferralProgramId = null,
    string? ReferralProgramSlug = null,
    bool CanManage = false,
    bool IsActive = false,
    bool IsAnalytics = false,
    UiText? SignupStatusText = null,
    DateTimeOffset? DetailInstant = null,
    string? ProgramName = null,
    IUiLocalization? Localization = null)
{
  private IUiLocalization EffectiveLocalization => Localization ?? UiLocalization.English;

  public string Title => EffectiveLocalization.Resolve(TitleText);

  public string Detail => DetailInstant is DateTimeOffset instant
      ? EffectiveLocalization.FormatDateTime(instant, TimeZoneInfo.Local)
      : EffectiveLocalization.Resolve(DetailText);

  public string? LocalizedSignupStatus => SignupStatusText is UiText text
      ? EffectiveLocalization.Resolve(text)
      : null;
}

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed record ProfileReferralLinkRow(
    string Id,
    UiText TitleText,
    string ReferralProgramId,
    string ReferralProgramSlug,
    IUiLocalization? Localization = null)
{
  public string Title => (Localization ?? UiLocalization.English).Resolve(TitleText);

  public static ProfileReferralLinkRow From(ReferralLink link, IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(link);
    var title = string.IsNullOrWhiteSpace(link.Label) ? link.ReferralProgramName : link.Label;
    return new(link.Id, UiText.UserContent(title), link.ReferralProgramId, link.ReferralProgramSlug, localization);
  }
}

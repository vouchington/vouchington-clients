using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Profiles;

public sealed record ProfileReferralLinkRow(
    string Id,
    string Title,
    string ReferralProgramId,
    string ReferralProgramSlug)
{
  public static ProfileReferralLinkRow From(ReferralLink link)
  {
    ArgumentNullException.ThrowIfNull(link);
    var title = string.IsNullOrWhiteSpace(link.Label) ? link.ReferralProgramName : link.Label;
    return new(link.Id, title, link.ReferralProgramId, link.ReferralProgramSlug);
  }
}

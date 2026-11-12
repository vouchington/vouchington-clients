using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.LandingPages;

public sealed record PublicLandingPageTarget(string? ReviewIdOrSlug, Uri? ExternalUrl)
{
  public static PublicLandingPageTarget? For(LandingPageItem? item) => item switch
  {
    LandingPageReviewItem review => new(string.IsNullOrWhiteSpace(review.Review.Slug) ? review.Review.Id : review.Review.Slug, null),
    LandingPageProfileLinkItem profile => External(ProfileUrl(profile.ProfileLink)),
    LandingPageReferralLinkItem referral => External(referral.ReferralLink.Url),
    LandingPageLinkItem link => External(link.Url),
    _ => null,
  };

  public static string? MemberId(LandingPageItem item)
  {
    ArgumentNullException.ThrowIfNull(item);
    return item switch
    {
      LandingPageReviewItem => item.Id,
      LandingPageReferralLinkItem => item.Id,
      _ => null,
    };
  }

  public static UiText Label(LandingPageItem item) => UiText.UserContent(item switch
  {
    LandingPageReviewItem review => string.IsNullOrWhiteSpace(review.Review.Title) ? review.Review.Markdown : review.Review.Title,
    LandingPageProfileLinkItem profile => FirstNonBlank(profile.ProfileLink.Name, profile.ProfileLink.Handle, ProfileUrl(profile.ProfileLink)?.ToString()),
    LandingPageReferralLinkItem referral => referral.ReferralLink.Label ?? referral.ReferralLink.ReferralProgramName,
    LandingPageLinkItem link => link.Label,
    LandingPageTopicGroupItem group => group.Topic.Name,
    _ => null,
  });

  private static string? FirstNonBlank(params string?[] values) =>
      values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

  private static PublicLandingPageTarget? External(Uri? url) =>
      url is { IsAbsoluteUri: true, Scheme: "https" or "http" } ? new(null, url) : null;

  private static Uri? ProfileUrl(LandingPageProfileLink link)
  {
    if (link.LinkType == "url") return link.Url;
    var prefix = link.LinkType switch
    {
      "twitter" or "x" => "https://x.com/",
      "facebook" => "https://www.facebook.com/",
      "instagram" => "https://www.instagram.com/",
      "github" => "https://github.com/",
      "linkedin" => "https://www.linkedin.com/in/",
      "youtube" => "https://www.youtube.com/@",
      "tiktok" => "https://www.tiktok.com/@",
      _ => null,
    };
    return prefix is null || string.IsNullOrWhiteSpace(link.Handle) ? null
        : new Uri(prefix + Uri.EscapeDataString(link.Handle.TrimStart('@')));
  }
}

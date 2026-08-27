using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.LandingPages;

public static class LandingPageItemMappings
{
  public static LandingPageItemInput ToInput(this LandingPageItem item)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.Type switch
    {
      "profile_link" when item is LandingPageProfileLinkItem profile => new LandingPageProfileLinkItemInput(profile.ProfileLink.Id),
      "review" when item is LandingPageReviewItem review => new LandingPageReviewItemInput(review.Review.Id),
      "referral_link" when item is LandingPageReferralLinkItem referral => new LandingPageReferralLinkItemInput(referral.ReferralLink.Id),
      "topic_group" when item is LandingPageTopicGroupItem group => new LandingPageTopicGroupItemInput(
          group.Topic.Id,
          group.Entries.Select(ToInput).ToArray()),
      "link" when item is LandingPageLinkItem link => new LandingPageLinkItemInput(link.Label, link.Url),
      _ => throw new InvalidOperationException($"Unsupported landing page item type '{item.Type}'."),
    };
  }

  public static string Describe(this LandingPageItem item) => UiLocalization.English.Resolve(item.DescribeText());

  public static UiText DescribeText(this LandingPageItem item)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.Type switch
    {
      "profile_link" when item is LandingPageProfileLinkItem profile =>
          UiText.Verbatim(profile.ProfileLink.Name ?? profile.ProfileLink.Handle ?? profile.ProfileLink.Url?.ToString() ?? UiLocalization.English.Localize(UiMessageKey.NativeDotnetResidualProfileLink)),
      "review" when item is LandingPageReviewItem review => UiText.Verbatim(review.Review.Title),
      "referral_link" when item is LandingPageReferralLinkItem referral => UiText.Verbatim(referral.ReferralLink.Label ?? referral.ReferralLink.ReferralProgramName),
      "topic_group" when item is LandingPageTopicGroupItem group => UiText.Localized(UiMessageKey.NativeDotnetResidualTopicGroup, ("topic", group.Topic.Name)),
      "link" when item is LandingPageLinkItem link => UiText.Verbatim(link.Label),
      _ => UiText.Verbatim(item.Type),
    };
  }

  public static string Describe(this LandingPageItemInput item) => UiLocalization.English.Resolve(item.DescribeText());

  public static UiText DescribeText(this LandingPageItemInput item)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.Type switch
    {
      "profile_link" => item.ProfileLinkId is { } profileLinkId ? UiText.Verbatim(profileLinkId) : UiText.Localized(UiMessageKey.NativeDotnetResidualProfileLink),
      "review" => item.ReviewId is { } reviewId ? UiText.Verbatim(reviewId) : UiText.Localized(UiMessageKey.NativeDotnetResidualReview),
      "referral_link" => item.ReferralLinkId is { } referralLinkId ? UiText.Verbatim(referralLinkId) : UiText.Localized(UiMessageKey.NativeDotnetResidualReferralLink),
      "topic_group" => UiText.Localized(UiMessageKey.NativeDotnetResidualTopicGroup, ("topic", item.TopicId ?? UiLocalization.English.Localize(UiMessageKey.NativeDotnetResidualTopic))),
      "link" => UiText.Verbatim(item.Label ?? item.Url?.ToString() ?? UiLocalization.English.Localize(UiMessageKey.NativeDotnetResidualLink)),
      _ => UiText.Verbatim(item.Type),
    };
  }
}

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static class ApiFixtureLandingPageItems
{
  public static ApiRequest Request() =>
      VouchaApiEndpoints.ReplaceMyLandingPageItems("landing-page-1", Body());

  public static ReplaceLandingPageItemsBody Body() => new([
      new LandingPageProfileLinkItemInput("profile-link-1"),
      new LandingPageReviewItemInput("review-1"),
      new LandingPageReferralLinkItemInput("referral-link-1"),
      new LandingPageTopicGroupItemInput(
          "topic-1",
          [
            new LandingPageReviewItemInput("review-2"),
            new LandingPageReferralLinkItemInput("referral-link-2"),
          ]),
      new LandingPageLinkItemInput("Newsletter", new Uri("https://example.com/newsletter")),
  ]);
}

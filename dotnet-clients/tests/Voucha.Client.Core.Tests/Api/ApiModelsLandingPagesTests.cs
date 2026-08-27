using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiModelsLandingPagesTests
{
  [Fact]
  public void LandingPageApiModelsExposeConstructedValues()
  {
    var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
    var profileLink = new LandingPageProfileLink(
        "profile-link-1",
        "user-abc",
        "website",
        1,
        "url-profile-1",
        new Uri("https://example.com"),
        "example",
        "Website",
        null,
        now,
        now);
    var review = new LandingPageReview(
        "review-1",
        "Best Travel Card",
        "best-travel-card",
        "Useful review",
        now,
        [new LandingPageReviewTopicRating("topic-1", "Travel Cards", "travel-cards", 5, 0)]);
    var referralLink = new LandingPageReferralLink(
        "referral-link-1",
        "topic-1",
        "Travel Cards",
        "travel-cards",
        "Apply",
        new Uri("https://example.com/apply"));
    var topic = new LandingPageTopic("topic-1", "Travel Cards", "travel-cards", "referral_program");
    var page = new LandingPage(
        "landing-page-1",
        "user-abc",
        "My Links",
        "Cards and reviews I recommend",
        "links",
        true,
        now,
        now,
        [
          new LandingPageProfileLinkItem("item-profile-1", profileLink),
          new LandingPageReviewItem("item-review-1", review),
          new LandingPageReferralLinkItem("item-referral-1", referralLink),
          new LandingPageTopicGroupItem(
              "item-topic-group-1",
              topic,
              [
                new LandingPageReviewItem("group-entry-review-1", review),
                new LandingPageReferralLinkItem("group-entry-referral-1", referralLink),
              ]),
          new LandingPageLinkItem("item-link-1", "Newsletter", new Uri("https://example.com/newsletter")),
        ]);

    var pagesResponse = new LandingPagesResponse([page], new PageInfo("cursor-1", true, "cursor-0"));
    var detailResponse = new LandingPageDetailResponse(page);
    var response = new LandingPageResponse(page);
    var candidates = new LandingPageCandidates(true, [profileLink], [review], [referralLink]);
    var candidatesResponse = new LandingPageCandidatesResponse(candidates);

    Assert.Equal("landing-page-1", page.Id);
    Assert.Equal("user-abc", page.UserId);
    Assert.Equal("My Links", page.Title);
    Assert.Equal("Cards and reviews I recommend", page.Subtitle);
    Assert.Equal("links", page.Slug);
    Assert.True(page.IsDefault);
    Assert.Equal(now, page.CreatedAt);
    Assert.Equal(now, page.UpdatedAt);
    Assert.NotNull(page.Items);
    Assert.Equal(5, page.Items.Count);

    Assert.Same(page, pagesResponse.Results[0]);
    Assert.Same(page, detailResponse.LandingPage);
    Assert.Same(page, response.LandingPage);
    Assert.Same(candidates, candidatesResponse.Candidates);
    Assert.True(candidates.CanCreateLandingPages);
    Assert.Same(profileLink, candidates.ProfileLinks[0]);
    Assert.Same(review, candidates.Reviews[0]);
    Assert.Same(referralLink, candidates.ReferralLinks[0]);

    var profileItem = Assert.IsType<LandingPageProfileLinkItem>(page.Items[0]);
    var reviewItem = Assert.IsType<LandingPageReviewItem>(page.Items[1]);
    var referralItem = Assert.IsType<LandingPageReferralLinkItem>(page.Items[2]);
    var topicGroupItem = Assert.IsType<LandingPageTopicGroupItem>(page.Items[3]);
    var linkItem = Assert.IsType<LandingPageLinkItem>(page.Items[4]);

    Assert.Equal("item-profile-1", profileItem.Id);
    Assert.Same(profileLink, profileItem.ProfileLink);
    Assert.Equal("item-review-1", reviewItem.Id);
    Assert.Same(review, reviewItem.Review);
    Assert.Equal("item-referral-1", referralItem.Id);
    Assert.Same(referralLink, referralItem.ReferralLink);
    Assert.Equal("item-topic-group-1", topicGroupItem.Id);
    Assert.Same(topic, topicGroupItem.Topic);
    Assert.Equal(2, topicGroupItem.Entries.Count);
    Assert.Equal("item-link-1", linkItem.Id);
    Assert.Equal("Newsletter", linkItem.Label);
    Assert.Equal(new Uri("https://example.com/newsletter"), linkItem.Url);

    Assert.Equal("profile_link", profileItem.Type);
    Assert.Equal("review", reviewItem.Type);
    Assert.Equal("referral_link", referralItem.Type);
    Assert.Equal("topic_group", topicGroupItem.Type);
    Assert.Equal("link", linkItem.Type);
    Assert.Equal("review", topicGroupItem.Entries[0].Type);
    Assert.Equal("referral_link", topicGroupItem.Entries[1].Type);
  }
}

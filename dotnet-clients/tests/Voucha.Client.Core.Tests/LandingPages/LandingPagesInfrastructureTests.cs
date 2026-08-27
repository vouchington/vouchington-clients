using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesInfrastructureTests
{
  [Fact]
  public async Task ApiLandingPagesServiceDelegatesToExpectedRoutes()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.landing-pages.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.landing-page-candidates.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.landing-page-detail.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.landing-page-detail.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.landing-page-detail.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.landing-page-detail.default")),
        new RecordedResponse("{}"),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.landing-page-detail.default")),
    ]);
    var service = new ApiLandingPagesService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await service.FetchPagesAsync(TestContext.Current.CancellationToken);
    await service.FetchCandidatesAsync(TestContext.Current.CancellationToken);
    await service.FetchDetailAsync("landing-page-1", TestContext.Current.CancellationToken);
    await service.CreateAsync(
        new CreateLandingPageBody("New Page", JsonNullableString.Null, "new-page"),
        TestContext.Current.CancellationToken);
    await service.UpdateAsync(
        "landing-page-1",
        new UpdateLandingPageBody(Title: "Updated", Subtitle: JsonNullableString.Null, Slug: "updated"),
        TestContext.Current.CancellationToken);
    await service.SetDefaultAsync("landing-page-1", TestContext.Current.CancellationToken);
    await service.DeleteAsync("landing-page-2", TestContext.Current.CancellationToken);
    await service.ReplaceItemsAsync(
        "landing-page-1",
        new ReplaceLandingPageItemsBody([
            new LandingPageProfileLinkItemInput("profile-link-1"),
            new LandingPageReviewItemInput("review-1"),
            new LandingPageReferralLinkItemInput("referral-link-1"),
            new LandingPageTopicGroupItemInput(
                "topic-1",
                [
                  new LandingPageReviewItemInput("review-1"),
                  new LandingPageReferralLinkItemInput("referral-link-1"),
                ]),
            new LandingPageLinkItemInput("Newsletter", new Uri("https://example.com/newsletter")),
        ]),
        TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request =>
        {
          Assert.Equal(HttpMethod.Get, request.Method);
          Assert.Equal("/api/v1/my/landing-pages", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Get, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/candidates", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Get, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-1", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Post, request.Method);
          Assert.Equal("/api/v1/my/landing-pages", request.PathAndQuery);
          Assert.Contains("\"title\":\"New Page\"", request.Body!, StringComparison.Ordinal);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Patch, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-1", request.PathAndQuery);
          Assert.Contains("\"slug\":\"updated\"", request.Body!, StringComparison.Ordinal);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Patch, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-1", request.PathAndQuery);
          Assert.Equal("""{"is_default":true}""", request.Body);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Delete, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-2", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Put, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-1/items", request.PathAndQuery);
          Assert.Contains("\"profile_link_id\":\"profile-link-1\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"review_id\":\"review-1\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"referral_link_id\":\"referral-link-1\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"topic_id\":\"topic-1\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"label\":\"Newsletter\"", request.Body!, StringComparison.Ordinal);
        });
  }

  [Fact]
  public void ApiLandingPagesServiceRejectsNullClient()
  {
    Assert.Throws<ArgumentNullException>(() => new ApiLandingPagesService(null!));
  }

  [Fact]
  public void LandingPageRowFromPageCopiesValues()
  {
    var page = new LandingPage(
        "landing-page-1",
        "user-abc",
        "My Links",
        null,
        "links",
        true,
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
        DateTimeOffset.Parse("2026-06-29T10:00:00Z"));

    var row = LandingPageRow.FromPage(page);

    Assert.Equal(page.Id, row.Id);
    Assert.Equal(page.Title, row.Title);
    Assert.Equal(page.Subtitle, row.Subtitle);
    Assert.Equal(page.Slug, row.Slug);
    Assert.Equal(page.IsDefault, row.IsDefault);
    Assert.Equal(page.CreatedAt, row.CreatedAt);
    Assert.Equal(page.UpdatedAt, row.UpdatedAt);
  }

  [Fact]
  public void LandingPageItemMappingsConvertKnownAndUnknownItems()
  {
    var profileLink = new LandingPageProfileLink(
        "profile-link-1",
        "user-abc",
        "website",
        1,
        "url-profile-1",
        new Uri("https://example.com"),
        null,
        null,
        null,
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"));
    var review = new LandingPageReview(
        "review-1",
        "Best Travel Card",
        "best-travel-card",
        "Useful review",
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
        [new LandingPageReviewTopicRating("topic-1", "Travel Cards", "travel-cards", 5, 0)]);
    var referralLink = new LandingPageReferralLink(
        "referral-link-1",
        "topic-1",
        "Travel Cards",
        "travel-cards",
        null,
        new Uri("https://example.com/apply"));
    var topicGroup = new LandingPageTopicGroupItem(
        "topic-group-1",
        new LandingPageTopic("topic-1", "Travel Cards", "travel-cards", "referral_program"),
        [
          new LandingPageReviewItem("group-entry-review-1", review),
          new LandingPageReferralLinkItem("group-entry-referral-1", referralLink),
        ]);
    var profileItem = new LandingPageProfileLinkItem("item-profile-1", profileLink);
    var reviewItem = new LandingPageReviewItem("item-review-1", review);
    var referralItem = new LandingPageReferralLinkItem("item-referral-1", referralLink);
    var linkItem = new LandingPageLinkItem("item-link-1", "Newsletter", new Uri("https://example.com/newsletter"));
    var unknownItem = new UnknownLandingPageItem("unsupported");

    var profileInput = profileItem.ToInput();
    var reviewInput = reviewItem.ToInput();
    var referralInput = referralItem.ToInput();
    var topicGroupInput = topicGroup.ToInput();
    var linkInput = linkItem.ToInput();

    Assert.IsType<LandingPageProfileLinkItemInput>(profileInput);
    Assert.Equal("profile-link-1", profileInput.ProfileLinkId);
    Assert.Equal("https://example.com/", profileItem.Describe());

    Assert.IsType<LandingPageReviewItemInput>(reviewInput);
    Assert.Equal("review-1", reviewInput.ReviewId);
    Assert.Equal("Best Travel Card", reviewItem.Describe());

    Assert.IsType<LandingPageReferralLinkItemInput>(referralInput);
    Assert.Equal("referral-link-1", referralInput.ReferralLinkId);
    Assert.Equal("Travel Cards", referralItem.Describe());

    var typedGroup = Assert.IsType<LandingPageTopicGroupItemInput>(topicGroupInput);
    Assert.Equal("topic-1", typedGroup.TopicId);
    var typedGroupEntries = typedGroup.Entries ?? throw new InvalidOperationException("Expected topic group entries.");
    Assert.Equal(2, typedGroupEntries.Count);
    Assert.IsType<LandingPageReviewItemInput>(typedGroupEntries[0]);
    Assert.IsType<LandingPageReferralLinkItemInput>(typedGroupEntries[1]);
    Assert.Equal("Topic group: Travel Cards", topicGroup.Describe());

    Assert.IsType<LandingPageLinkItemInput>(linkInput);
    Assert.Equal("Newsletter", linkInput.Label);
    Assert.Equal("Newsletter", linkItem.Describe());

    Assert.Equal("https://example.com/profile", new LandingPageProfileLinkItem("item-profile-2", new LandingPageProfileLink(
        "profile-link-2",
        "user-abc",
        "website",
        1,
        "url-profile-2",
        new Uri("https://example.com/profile"),
        null,
        null,
        null,
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"))).Describe());
    Assert.Equal("Travel Cards", new LandingPageReferralLinkItem(
        "item-referral-2",
        new LandingPageReferralLink(
            "referral-link-2",
            "topic-2",
            "Travel Cards",
            "travel-cards",
            null,
            new Uri("https://example.com/apply-2"))).Describe());
    Assert.Equal("Topic group: Topic", new LandingPageItemInput("topic_group").Describe());
    Assert.Equal("unsupported", new LandingPageItemInput("unsupported").Describe());
    Assert.Throws<InvalidOperationException>(() => unknownItem.ToInput());
    Assert.Equal("unsupported", unknownItem.Describe());
    Assert.Throws<ArgumentNullException>(() => LandingPageRow.FromPage(null!));
    Assert.Throws<ArgumentNullException>(() => ((LandingPageItem)null!).ToInput());
    Assert.Throws<ArgumentNullException>(() => ((LandingPageItemInput)null!).Describe());
  }

  private sealed record UnknownLandingPageItem(string ItemId) : LandingPageItem(ItemId)
  {
    public override string Type => "unsupported";
  }
}

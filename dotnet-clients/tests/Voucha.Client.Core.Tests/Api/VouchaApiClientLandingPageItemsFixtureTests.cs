using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task ReplaceLandingPageItemsMatchesSharedFiveTypeFixture()
  {
    var (client, handler) = CreateClient("native.landing-page-items-mutation.default");
    var body = new ReplaceLandingPageItemsBody([
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

    var response = await client.ReplaceMyLandingPageItemsAsync(
        "landing-page-1",
        body,
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Put, "/api/v1/my/landing-pages/landing-page-1/items");
    var request = Assert.Single(handler.Requests);
    var expected = ApiFixtureLoader.ManifestFixtures
        .Single(entry => entry.Id == "native.landing-page-items-mutation.default").RequestBody;
    Assert.True(JsonNode.DeepEquals(
        JsonNode.Parse(expected!.Value.GetRawText()),
        JsonNode.Parse(request.Body!)));
    Assert.Equal(5, response.LandingPage.Items?.Count);
    Assert.IsType<LandingPageTopicGroupItem>(response.LandingPage.Items?[3]);
  }
}

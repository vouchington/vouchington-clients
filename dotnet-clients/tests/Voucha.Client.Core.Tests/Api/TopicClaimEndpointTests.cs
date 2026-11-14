using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class TopicClaimEndpointTests
{
  [Fact]
  public void TopicClaimListsForwardTheOpaqueCursor()
  {
    var mine = VouchaApiEndpoints.MyTopicClaims(after: "cursor-1", limit: 1);
    Assert.Equal("/api/v1/my/topic-claims", mine.Path);
    Assert.Equal("cursor-1", mine.Query["after"]);
    Assert.Equal("1", mine.Query["limit"]);

    var topic = VouchaApiEndpoints.TopicClaims(
        "topic-1",
        mineOnly: true,
        activeOnly: false,
        after: "cursor-2",
        limit: 2);
    Assert.Equal("/api/v1/topics/topic-1/claims", topic.Path);
    Assert.Equal("cursor-2", topic.Query["after"]);
    Assert.Equal("true", topic.Query["mine_only"]);
    Assert.Equal("false", topic.Query["active_only"]);
    Assert.Equal("2", topic.Query["limit"]);
  }

  [Fact]
  public async Task TopicClaimClientDecodesThePublishedPageAndForwardsTheCursor()
  {
    var mineHandler = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.my-topic-claims.page"));
    var client = Client(mineHandler);
    var mine = await client.FetchMyTopicClaimsAsync("cursor-1", 1, TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/my/topic-claims?after=cursor-1&limit=1", mineHandler.PathAndQuery);
    AssertPublishedPage(mine);

    var topicHandler = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.topic-claims.page"));
    client = Client(topicHandler);
    var topic = await client.FetchTopicClaimsAsync(
        "00000000-0000-7000-8000-000000000301",
        mineOnly: true,
        activeOnly: true,
        after: "cursor-2",
        limit: 1,
        TestContext.Current.CancellationToken);

    Assert.Equal(
        "/api/v1/topics/00000000-0000-7000-8000-000000000301/claims?active_only=true&after=cursor-2&limit=1&mine_only=true",
        topicHandler.PathAndQuery);
    AssertPublishedPage(topic);
  }

  private static void AssertPublishedPage(TopicClaimPage page)
  {
    var claim = Assert.Single(page.Results);
    Assert.Equal("00000000-0000-7000-8000-000000000401", claim.Id);
    Assert.Equal("00000000-0000-7000-8000-000000000301", claim.TopicId);
    Assert.Equal("00000000-0000-7000-8000-000000000001", claim.ClaimantUserId);
    Assert.Null(claim.VerificationMethod);
    Assert.Equal("Issuer", claim.ClaimedRole);
    Assert.Equal("Public registry entry", claim.Evidence);
    Assert.Null(claim.SubmittedAt);
    Assert.Null(claim.VerifiedAt);
    Assert.Null(claim.VerifiedById);
    Assert.Null(claim.RejectedAt);
    Assert.Null(claim.RejectedById);
    Assert.Null(claim.RejectionReason);
    Assert.Null(claim.RevokedAt);
    Assert.Null(claim.RevokedById);
    Assert.Null(claim.RevocationReason);
    Assert.Null(claim.VerificationHostnameId);
    Assert.Null(claim.VerificationTokenIssuedAt);
    Assert.Null(claim.DomainVerifiedAt);
    Assert.Equal("2026-07-01T00:00:00Z", claim.CreatedAt);
    Assert.Equal("2026-07-01T00:00:00Z", claim.UpdatedAt);
    Assert.Equal("pending", claim.State);
    Assert.False(page.PageInfo.HasNextPage);
    Assert.Null(page.PageInfo.EndCursor);
    Assert.Null(page.PageInfo.StartCursor);
  }

  private static VouchaApiClient Client(RecordingHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
}

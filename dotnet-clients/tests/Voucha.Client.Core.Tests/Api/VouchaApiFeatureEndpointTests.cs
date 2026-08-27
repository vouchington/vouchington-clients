using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiFeatureEndpointTests
{
  [Fact]
  public void AllPostsIncludesWebParityFilters()
  {
    var request = VouchaApiEndpoints.AllPosts(new FetchPostsRequest(
        Query: "rewards",
        PostTypes: "review,discussion",
        Topics: "topic-1",
        ReviewTopic: "topic-2",
        Creator: "alice",
        DataPointVertical: "credit_card",
        Sort: "best",
        After: "cursor",
        Limit: 12,
        SemanticSearchQuery: "travel rewards"));

    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/posts", request.Path);
    Assert.Equal("rewards", request.Query["q"]);
    Assert.Equal("review,discussion", request.Query["post_types"]);
    Assert.Equal("topic-1", request.Query["topics"]);
    Assert.Equal("topic-2", request.Query["review_topic"]);
    Assert.Equal("alice", request.Query["creator"]);
    Assert.Equal("credit_card", request.Query["data_point_vertical"]);
    Assert.Equal("best", request.Query["sort"]);
    Assert.Equal("cursor", request.Query["after"]);
    Assert.Equal("12", request.Query["limit"]);
    Assert.Equal("travel rewards", request.Query["semantic_search_query"]);
  }

  [Fact]
  public void PostDetailShareSendAndReferralValidationRoutesEncodePathSegments()
  {
    Assert.Equal("/api/v1/posts/post%201", VouchaApiEndpoints.Post("post 1").Path);
    Assert.Equal("/api/v1/posts/post%201/shares", VouchaApiEndpoints.SharePostWithFollowers("post 1").Path);
    Assert.Equal("/api/v1/posts/post%201/sends", VouchaApiEndpoints.SendPostToFollowers(
        "post 1",
        FollowerDistributionBody.AllFollowers()).Path);
    Assert.Null(VouchaApiEndpoints.SharePostWithFollowers("post 1").Body);
    Assert.Equal(
        "/api/v1/topics/program%201/referral-program/validation-info",
        VouchaApiEndpoints.ReferralProgramValidationInfo("program 1").Path);
  }

  [Fact]
  public void FollowerDistributionRejectsInvalidSelectedRecipients()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => FollowerDistributionBody.Selected([]));
    Assert.Throws<ArgumentOutOfRangeException>(() => FollowerDistributionBody.Selected(Enumerable.Repeat("user", 101).ToArray()));
    Assert.Throws<ArgumentException>(() => FollowerDistributionBody.Selected(["not-a-uuid"]));
    Assert.Throws<ArgumentException>(() => FollowerDistributionBody.Selected(["00000000000070008000000000000001"]));
    Assert.Throws<ArgumentException>(() => FollowerDistributionBody.Selected(["{00000000-0000-7000-8000-000000000001}"]));
    Assert.Throws<ArgumentException>(() => FollowerDistributionBody.Selected(["00000000-0000-7000-8000-000000000001", "00000000-0000-7000-8000-000000000001"]));
    Assert.Equal("all_followers", FollowerDistributionBody.AllFollowers().Audience);
    Assert.Equal("selected_followers", FollowerDistributionBody.Selected(["00000000-0000-7000-8000-000000000001"]).Audience);
  }

  [Fact]
  public void FollowerSearchCarriesQueryAndCursor()
  {
    var request = VouchaApiEndpoints.UserFollowers("user-1", 25, "cursor", "needle");
    Assert.Equal("needle", request.Query["q"]);
    Assert.Equal("cursor", request.Query["after"]);
  }
}

namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest AllPosts(FetchPostsRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);
    return Get(
        "/api/v1/posts",
        Query(
            ("q", request.Query),
            ("post_types", request.PostTypes),
            ("topics", request.Topics),
            ("review_topic", request.ReviewTopic),
            ("creator", request.Creator),
            ("data_point_vertical", request.DataPointVertical),
            ("sort", request.Sort),
            ("limit", request.Limit),
            ("after", request.After),
            ("semantic_search_query", request.SemanticSearchQuery)));
  }

  public static ApiRequest Post(string postIdOrSlug) =>
      Get($"/api/v1/posts/{Path(postIdOrSlug)}");

  public static ApiRequest SharePostWithFollowers(string postIdOrSlug) =>
      new(HttpMethod.Post, $"/api/v1/posts/{Path(postIdOrSlug)}/shares");

  public static ApiRequest SendPostToFollowers(
      string postIdOrSlug,
      FollowerDistributionBody body) =>
      new(HttpMethod.Post, $"/api/v1/posts/{Path(postIdOrSlug)}/sends") { Body = body };

  public static ApiRequest Report(ReportBody body) =>
      new(HttpMethod.Post, "/api/v1/reports") { Body = body };

  public static ApiRequest ReferralProgramValidationInfo(string topicIdOrSlug) =>
      Get($"/api/v1/topics/{Path(topicIdOrSlug)}/referral-program/validation-info");
}

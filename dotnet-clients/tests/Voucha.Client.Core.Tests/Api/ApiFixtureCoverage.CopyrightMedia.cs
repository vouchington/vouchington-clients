using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  public static Dictionary<string, Type> WithCopyrightMediaFixtures(
      this Dictionary<string, Type> registry)
  {
    registry["native.posts.images.placement.default"] = typeof(PostImagePlacementResponse);
    registry["native.moderation.copyright.image-similarity-candidates.default"] =
        typeof(CopyrightImageSimilarityCandidatesResponse);
    return registry;
  }

  public static Dictionary<string, ApiRequest> WithCopyrightMediaEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.posts.images.placement.default"] = VouchaApiEndpoints.PostImages(
        ApiFixtureLoader.RouteParameterValue("native.posts.images.placement.default", "idOrSlug"));
    registry["native.moderation.copyright.image-similarity-candidates.default"] =
        VouchaApiEndpoints.CopyrightImageSimilarityCandidates(
            ApiFixtureLoader.RouteParameterValue(
                "native.moderation.copyright.image-similarity-candidates.default", "id"),
            ApiFixtureLoader.RouteParameterValue(
                "native.moderation.copyright.image-similarity-candidates.default", "targetId"));
    return registry;
  }
}

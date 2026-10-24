using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  public static Dictionary<string, Type> WithTopicClaimFixtures(this Dictionary<string, Type> registry)
  {
    registry["native.my-topic-claims.page"] = typeof(TopicClaimPage);
    registry["native.topic-claims.page"] = typeof(TopicClaimPage);
    return registry;
  }

  private static Dictionary<string, ApiRequest> WithTopicClaimEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.my-topic-claims.page"] = VouchaApiEndpoints.MyTopicClaims(limit: 1);
    registry["native.topic-claims.page"] = VouchaApiEndpoints.TopicClaims(
        "00000000-0000-7000-8000-000000000301",
        mineOnly: true,
        activeOnly: true,
        limit: 1);
    return registry;
  }
}

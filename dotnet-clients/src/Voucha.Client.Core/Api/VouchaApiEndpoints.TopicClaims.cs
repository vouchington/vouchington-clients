namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest MyTopicClaims(string? after = null, int limit = 25) =>
      Get("/api/v1/my/topic-claims", Query(("after", after), ("limit", limit)));

  public static ApiRequest TopicClaims(
      string idOrSlug,
      bool? mineOnly = null,
      bool? activeOnly = null,
      string? after = null,
      int limit = 25) =>
      Get(
          $"/api/v1/topics/{Path(idOrSlug)}/claims",
          Query(
              ("after", after),
              ("limit", limit),
              ("mine_only", Bool(mineOnly)),
              ("active_only", Bool(activeOnly))));
}

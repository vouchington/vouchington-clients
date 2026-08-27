namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest SearchPosts(string query, int limit = 10) =>
      Get("/api/v1/posts", Query(("q", query), ("limit", limit)));

  public static ApiRequest VoteEntityRelation(string relationId, ElectionVoteChoice choice)
  {
    ElectionVotePolicy.Relation.Require(choice);
    return new(HttpMethod.Put, $"/api/v1/entity-relations/{Path(relationId)}/vote")
    {
      Body = new ElectionVoteBody(choice),
    };
  }

  public static ApiRequest ClearEntityRelationVote(string relationId) =>
      new(HttpMethod.Delete, $"/api/v1/entity-relations/{Path(relationId)}/vote");

  public static ApiRequest PublisherTypes() => Get("/api/v1/topics/publisher-types");

  public static ApiRequest UserTags() => Get("/api/v1/topics/user-tags");
}

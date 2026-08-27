namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest FollowTopic(string topicId) =>
      new(HttpMethod.Put, $"/api/v1/bookmarks/topic/{Path(topicId)}/follow");

  public static ApiRequest UnfollowTopic(string topicId) =>
      new(HttpMethod.Delete, $"/api/v1/bookmarks/topic/{Path(topicId)}/follow");

  public static ApiRequest TopicAdditionalHostnames(string topicId, string? after = null, int? limit = null) =>
      Get(
          $"/api/v1/topics/{Path(topicId)}/additional-hostnames",
          Query(("after", after), ("limit", limit)));

  public static ApiRequest CreateTopicAdditionalHostname(string topicId, string hostname) =>
      new(HttpMethod.Post, $"/api/v1/topics/{Path(topicId)}/additional-hostnames")
      {
        Body = new { hostname },
      };

  public static ApiRequest DeleteTopicAdditionalHostname(string topicId, string hostnameId) =>
      new(HttpMethod.Delete, $"/api/v1/topics/{Path(topicId)}/additional-hostnames/{Path(hostnameId)}");
}

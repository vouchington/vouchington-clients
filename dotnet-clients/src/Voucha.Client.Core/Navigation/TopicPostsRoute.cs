using Voucha.Client.Core.Api;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.Core.Navigation;

public sealed record TopicPostsRoute(string TopicIdOrSlug, string Section)
{
  public static TopicPostsRoute? FromResolution(NativeDeepLinkResolution resolution)
  {
    if (resolution.DestinationId is not (NativeRouteDestinationId.TopicDetail or NativeRouteDestinationId.SourceDetail) ||
        resolution.Match is not { } match || match.Param("idOrSlug", "id") is not { Length: > 0 } topic)
      return null;
    var section = match.Template.Split('/').Last();
    return section is "posts" or "discussions" or "reviews" or "data-points"
        ? new(topic, section) : null;
  }

  public async Task<FetchPostsRequest> ResolveRequestAsync(
      ITopicsService topics, CancellationToken cancellationToken = default)
  {
    var response = await topics.FetchTopicAsync(TopicIdOrSlug, cancellationToken).ConfigureAwait(false);
    return RequestForTopic(response.Topic.Id);
  }

  public FetchPostsRequest RequestForTopic(string topicId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
    return Section switch
    {
      "posts" or "discussions" => new(Topics: topicId),
      "reviews" => new(PostTypes: "review", ReviewTopic: topicId),
      "data-points" => new(PostTypes: "data_point", DataPointTopic: topicId),
      _ => throw new InvalidOperationException("Unsupported topic post section."),
    };
  }
}

namespace Voucha.Client.Core.Navigation;

public static class TopicDetailRoute
{
  public static bool TryGetTopicId(
      NativeRouteDestinationId? destinationId,
      NativeRouteMatch? match,
      out string topicId)
  {
    topicId = match?.Param("idOrSlug", "id") ?? string.Empty;
    return topicId.Length > 0 &&
        destinationId is NativeRouteDestinationId.TopicDetail or NativeRouteDestinationId.SourceDetail;
  }
}

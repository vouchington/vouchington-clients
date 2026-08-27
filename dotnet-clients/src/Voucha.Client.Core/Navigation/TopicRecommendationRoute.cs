namespace Voucha.Client.Core.Navigation;

public static class TopicRecommendationRoute
{
  public static bool TryGetDetailId(NativeRouteMatch? match, out string recommendationId)
  {
    recommendationId = match?.Param("id") ?? string.Empty;
    return recommendationId.Length > 0 &&
        match?.Path.StartsWith("/topic-recommendations/", StringComparison.Ordinal) == true &&
        !match.Path.EndsWith("/edit", StringComparison.Ordinal);
  }
}

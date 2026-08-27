using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Tags;

public static class TagManagementRoutes
{
  internal static readonly string[] PostTypes =
  [
    "review",
    "discussion",
    "story",
    "article",
    "blog-post",
    "link",
    "data-point",
  ];

  internal static readonly string[] TopicTypes =
  [
    "bank-account",
    "card",
    "instance",
    "referral-program",
    "rewards-program",
    "rewards-program-status",
    "source",
    "topic",
  ];

  private static readonly NativeRoutePattern[] PostPatterns =
  [
    new("/review/:id/tags/:objectType"),
    new("/discussion/:id/tags/:objectType"),
    new("/story/:id/tags/:objectType"),
    new("/article/:id/tags/:objectType"),
    new("/blog-post/:id/tags/:objectType"),
    new("/link/:id/tags/:objectType"),
    new("/data-point/:id/tags/:objectType"),
  ];

  private static readonly NativeRoutePattern[] TopicPatterns =
  [
    new("/bank-account/:idOrSlug/tags/:objectType"),
    new("/card/:idOrSlug/tags/:objectType"),
    new("/instance/:idOrSlug/tags/:objectType"),
    new("/referral-program/:idOrSlug/tags/:objectType"),
    new("/rewards-program/:idOrSlug/tags/:objectType"),
    new("/rewards-program-status/:idOrSlug/tags/:objectType"),
    new("/source/:idOrSlug/tags/:objectType"),
    new("/topic/:idOrSlug/tags/:objectType"),
  ];

  private static readonly NativeRoutePattern[] RssFeedItemPatterns =
  [
    new("/rss-feed-items/:id/tags/topic"),
  ];

  public static bool TryResolve(string path, out TagManagementRouteContext context)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    foreach (var pattern in PostPatterns)
    {
      if (pattern.Match(path) is not { } match)
      {
        continue;
      }

      var id = match.Param("id");
      if (string.IsNullOrWhiteSpace(id))
      {
        continue;
      }

      context = new TagManagementRouteContext("post", id, match.Param("objectType") ?? "topic");
      return true;
    }

    foreach (var pattern in TopicPatterns)
    {
      if (pattern.Match(path) is not { } match)
      {
        continue;
      }

      var idOrSlug = match.Param("idOrSlug");
      if (string.IsNullOrWhiteSpace(idOrSlug))
      {
        continue;
      }

      context = new TagManagementRouteContext("topic", idOrSlug, match.Param("objectType") ?? "topic");
      return true;
    }

    foreach (var pattern in RssFeedItemPatterns)
    {
      if (pattern.Match(path) is not { } match)
      {
        continue;
      }

      var id = match.Param("id");
      if (string.IsNullOrWhiteSpace(id))
      {
        continue;
      }

      context = new TagManagementRouteContext("rss_feed_item", id, "topic");
      return true;
    }

    context = null!;
    return false;
  }
}

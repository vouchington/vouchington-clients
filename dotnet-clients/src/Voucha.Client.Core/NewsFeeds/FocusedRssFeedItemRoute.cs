using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.NewsFeeds;

public static class FocusedRssFeedItemRoute
{
  public static bool TryResolve(
      NativeRouteMatch? match,
      out string itemId,
      out NewsFeedItemKind kind)
  {
    itemId = match?.QueryValue("rss_item")?.Trim() ?? string.Empty;
    kind = match?.Path switch
    {
      "/news" => NewsFeedItemKind.Article,
      "/podcast-episodes" or "/videos" => NewsFeedItemKind.Media,
      _ => NewsFeedItemKind.Article,
    };
    return !string.IsNullOrWhiteSpace(itemId) && match?.Path is "/news" or "/podcast-episodes" or "/videos";
  }
}

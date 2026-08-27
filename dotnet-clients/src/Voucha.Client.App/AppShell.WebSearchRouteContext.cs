using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private void SetWebSearchRouteContext(NativeDeepLinkResolution resolution)
  {
    if (resolution.IntentId != "web-search" || resolution.DestinationId is not { } destinationId)
    {
      return;
    }

    switch (destinationId)
    {
      case NativeRouteDestinationId.DomainDetail:
        webSearchRouteContextStore.Set(new(
            OmnisearchWebSearchSurface.Domains,
            resolution.Match?.Param("idOrHostname")));
        break;
      case NativeRouteDestinationId.DomainsBrowse:
        if (resolution.Match?.Template == "/domains/compare")
        {
          webSearchRouteContextStore.Clear();
          return;
        }

        webSearchRouteContextStore.Set(new(OmnisearchWebSearchSurface.Domains));
        break;
      case NativeRouteDestinationId.UrlDetail:
        if (resolution.Match?.Template?.StartsWith("/crawler/", StringComparison.Ordinal) == true)
        {
          return;
        }

        webSearchRouteContextStore.Set(new(
            OmnisearchWebSearchSurface.Urls,
            resolution.Match?.Param("id"),
            resolution.Match?.Param("crawlId"),
            resolution.Match?.Template == "/url/:id/crawls"
                ? OmnisearchWebSearchDetailKind.UrlCrawls
                : OmnisearchWebSearchDetailKind.Detail));
        break;
      case NativeRouteDestinationId.UrlsBrowse:
        switch (resolution.Match?.Template)
        {
          case "/my/urls/saved":
            webSearchRouteContextStore.Set(new(
                OmnisearchWebSearchSurface.Urls,
                "saved",
                DetailKind: OmnisearchWebSearchDetailKind.BookmarkedUrls));
            break;
          case "/my/domains/muted":
            webSearchRouteContextStore.Set(new(
                OmnisearchWebSearchSurface.Domains,
                "muted",
                DetailKind: OmnisearchWebSearchDetailKind.BookmarkedHostnames));
            break;
          case "/my/domains/blocked":
            webSearchRouteContextStore.Set(new(
                OmnisearchWebSearchSurface.Domains,
                "blocked",
                DetailKind: OmnisearchWebSearchDetailKind.BookmarkedHostnames));
            break;
          default:
            webSearchRouteContextStore.Set(new(OmnisearchWebSearchSurface.Urls));
            break;
        }
        break;
    }
  }
}

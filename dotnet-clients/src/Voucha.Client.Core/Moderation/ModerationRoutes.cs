using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public static class ModerationRoutes
{
  private static readonly Dictionary<string, ModerationRouteContext> Routes = new(StringComparer.Ordinal)
  {
    ["/reports"] = new("/reports", UiMessageKey.NativeDotnetModerationReports, ModerationRouteKind.Reports),
    ["/appeals"] = new("/appeals", UiMessageKey.NativeDotnetModerationAppeals, ModerationRouteKind.Appeals),
    ["/my/appeals"] = new("/my/appeals", UiMessageKey.NativeDotnetModerationMyAppeals, ModerationRouteKind.Appeals, Mine: true),
    ["/disputes"] = new("/disputes", UiMessageKey.NativeDotnetModerationReviewDisputes, ModerationRouteKind.Disputes),
    ["/my/disputes"] = new("/my/disputes", UiMessageKey.NativeDotnetModerationMyDisputes, ModerationRouteKind.Disputes, Mine: true),
    ["/posts/review-queue"] = new("/posts/review-queue", UiMessageKey.NativeDotnetModerationReviewQueue, ModerationRouteKind.ReviewQueue),
    ["/admin/modlog"] = new("/admin/modlog", UiMessageKey.NativeDotnetModerationModLog, ModerationRouteKind.AdminModlog),
    ["/admin/moderation-analytics"] = new("/admin/moderation-analytics", UiMessageKey.NativeDotnetModerationAnalytics, ModerationRouteKind.AdminAnalytics),
    ["/moderation-transparency"] = new("/moderation-transparency", UiMessageKey.NativeSwiftCommunityRowsModerationTransparency, ModerationRouteKind.Transparency),
    ["/my/warnings"] = new("/my/warnings", UiMessageKey.NativeDotnetModerationWarnings, ModerationRouteKind.PersonalCases, Mine: true, ApiPath: "/api/v1/my/warnings"),
    ["/my/bans"] = new("/my/bans", UiMessageKey.NativeDotnetModerationBans, ModerationRouteKind.PersonalCases, Mine: true, ApiPath: "/api/v1/my/bans"),
    ["/my/removed-posts"] = new("/my/removed-posts", UiMessageKey.NativeDotnetModerationRemovedPosts, ModerationRouteKind.PersonalCases, Mine: true, ApiPath: "/api/v1/my/removed-posts"),
    ["/my/account-status"] = new("/my/account-status", UiMessageKey.NativeDotnetModerationMyAppeals, ModerationRouteKind.PersonalCases, Mine: true),
  };

  public static bool TryResolve(string? path, [NotNullWhen(true)] out ModerationRouteContext? context)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      context = null;
      return false;
    }

    var queryStart = path.IndexOf('?', StringComparison.Ordinal);
    var routePath = queryStart < 0 ? path : path[..queryStart];
    if (!Routes.TryGetValue(routePath, out var route))
    {
      context = null;
      return false;
    }

    if (route.RouteKind != ModerationRouteKind.Transparency || queryStart < 0)
    {
      context = route;
      return true;
    }

    var range = ParseQueryValue(path[(queryStart + 1)..], "range");
    context = route with { TransparencyRange = ModerationTransparencyRange.ParseOrDefault(range) };
    return true;
  }

  public static bool TryResolve(
      NativeRouteMatch? match,
      [NotNullWhen(true)] out ModerationRouteContext? context)
  {
    if (match is null || !TryResolve(match.Path, out var route))
    {
      context = null;
      return false;
    }

    context = route.RouteKind == ModerationRouteKind.Transparency
        ? route with
        {
          TransparencyRange = ModerationTransparencyRange.ParseOrDefault(match.QueryValue("range")),
        }
        : route;
    return true;
  }

  private static string? ParseQueryValue(string query, string key) =>
      query.Split('&', StringSplitOptions.RemoveEmptyEntries)
          .Select(part => part.Split('=', 2))
          .FirstOrDefault(part => part.Length == 2 && string.Equals(part[0], key, StringComparison.Ordinal))?
          .ElementAtOrDefault(1);
}

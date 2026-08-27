namespace Voucha.Client.Core.Navigation;

public static class NativeUserProfileRoutePaths
{
  private static readonly string[] HistorySubpaths =
  [
    "/posts",
    "/reviews",
    "/discussions",
    "/comments",
  ];

  private static readonly string[] CollectionSubpaths =
  [
    "/topics/following",
    "/users/following",
    "/users/followers",
    "/rss-feeds/following",
    "/communities/member",
  ];

  public static bool IsUserProfilePath(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    if (!path.StartsWith("/user/", StringComparison.Ordinal)) return false;

    var subpath = Subpath(path);
    return subpath is null ||
        HistorySubpaths.Contains(subpath, StringComparer.OrdinalIgnoreCase) ||
        CollectionSubpaths.Contains(subpath, StringComparer.OrdinalIgnoreCase);
  }

  public static bool IsHistoryPath(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    if (!path.StartsWith("/user/", StringComparison.Ordinal)) return false;

    var subpath = Subpath(path);
    return subpath is null || HistorySubpaths.Contains(subpath, StringComparer.OrdinalIgnoreCase);
  }

  public static bool IsSupportedMatch(NativeRouteMatch match)
  {
    ArgumentNullException.ThrowIfNull(match);
    if (!IsUserProfilePath(match.Path)) return false;
    if (!string.Equals(Subpath(match.Path), "/rss-feeds/following", StringComparison.OrdinalIgnoreCase)) return true;
    return match.QueryValue("feed_type") is null or "" or "article" or "podcast" or "video";
  }

  public static string? Subpath(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    var separatorIndex = path.IndexOf('/', "/user/".Length);
    return separatorIndex < 0 ? null : path[separatorIndex..];
  }
}

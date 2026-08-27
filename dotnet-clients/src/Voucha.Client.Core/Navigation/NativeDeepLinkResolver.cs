namespace Voucha.Client.Core.Navigation;

public enum NativeDeepLinkStatus
{
  Included,
  Excluded,
  Unmapped,
  Invalid,
}

public sealed record NativeDeepLinkResolution(
    NativeDeepLinkStatus Status,
    string PathAndQuery,
    NativeRouteCatalogEntry? Entry,
    NativeRouteMatch? Match,
    NativeRouteDestinationId? DestinationId,
    string? IntentId,
    bool RequiresAuthentication,
    bool IsVisible,
    string? Reason,
    bool ViewerIsAuthenticated = false)
{
  public bool ShouldShowSignIn => DestinationId == NativeRouteDestinationId.SignIn;

  public bool CanNavigate => Status == NativeDeepLinkStatus.Included &&
      (ShouldShowSignIn || (IntentId is not null && IsVisible));

  public bool ShouldQueueUntilAuthenticated => Status == NativeDeepLinkStatus.Included &&
      RequiresAuthentication &&
      !IsVisible &&
      !ViewerIsAuthenticated;
}

public static partial class NativeDeepLinkResolver
{
  public static NativeDeepLinkResolution Resolve(string rawUrl, NavigationViewer viewer)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(rawUrl);
    ArgumentNullException.ThrowIfNull(viewer);

    bool normalized;
    string pathAndQuery;
    string? reason;
    if (rawUrl.StartsWith('/'))
    {
      normalized = TryNormalizeRelative(rawUrl, out pathAndQuery, out reason);
    }
    else if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
    {
      normalized = TryNormalize(uri, out pathAndQuery, out reason);
    }
    else
    {
      normalized = TryNormalizeRelative(rawUrl, out pathAndQuery, out reason);
    }
    if (!normalized)
    {
      return new(NativeDeepLinkStatus.Invalid, "/", null, null, null, null, false, false, reason);
    }

    return ResolveNormalized(pathAndQuery, viewer);
  }

  private static NativeDeepLinkResolution ResolveNormalized(string pathAndQuery, NavigationViewer viewer)
  {
    var route = NativeRouteCatalog.MatchingRoute(pathAndQuery);
    if (route is null)
    {
      return new(NativeDeepLinkStatus.Unmapped, pathAndQuery, null, null, null, null, false, false, null);
    }

    var entry = route.Value.Entry;
    var match = route.Value.Match;
    if (entry.DestinationId is not { } destinationId)
    {
      return new(NativeDeepLinkStatus.Excluded, pathAndQuery, entry, match, null, null, false, false, entry.ExclusionReason);
    }

    if (destinationId == NativeRouteDestinationId.UserProfile &&
        !NativeUserProfileRoutePaths.IsSupportedMatch(match))
    {
      return new(NativeDeepLinkStatus.Invalid, pathAndQuery, entry, match, destinationId, null, false, false, "Unsupported public profile scope.");
    }

    var intentId = IntentIdFor(destinationId, match);
    var requiresAuthentication = RequiresAuthentication(destinationId, match);
    var isPublicProfile = destinationId == NativeRouteDestinationId.UserProfile &&
        NativeUserProfileRoutePaths.IsSupportedMatch(match);
    var visible = (isPublicProfile || IsVisible(intentId, requiresAuthentication, viewer)) &&
        IsRouteVisible(destinationId, match, viewer);
    return new(NativeDeepLinkStatus.Included, pathAndQuery, entry, match, destinationId, intentId, requiresAuthentication, visible, null, viewer.IsAuthenticated);
  }

  public static NativeDeepLinkResolution Resolve(Uri uri, NavigationViewer viewer)
  {
    ArgumentNullException.ThrowIfNull(uri);
    ArgumentNullException.ThrowIfNull(viewer);

    if (!TryNormalize(uri, out var pathAndQuery, out var reason))
    {
      return new(NativeDeepLinkStatus.Invalid, "/", null, null, null, null, false, false, reason);
    }

    return ResolveNormalized(pathAndQuery, viewer);
  }

  public static bool TryNormalize(string rawUrl, out string pathAndQuery, out string? reason)
  {
    ArgumentNullException.ThrowIfNull(rawUrl);

    reason = null;
    if (rawUrl.StartsWith('/'))
    {
      pathAndQuery = rawUrl;
      return true;
    }

    if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
    {
      return TryNormalize(uri, out pathAndQuery, out reason);
    }

    pathAndQuery = "/" + rawUrl;
    return true;
  }

  private static bool TryNormalizeRelative(string rawPath, out string pathAndQuery, out string? reason)
  {
    ArgumentNullException.ThrowIfNull(rawPath);

    reason = null;
    pathAndQuery = rawPath.StartsWith('/') ? rawPath : "/" + rawPath;
    return true;
  }

  public static bool TryNormalize(Uri uri, out string pathAndQuery, out string? reason)
  {
    ArgumentNullException.ThrowIfNull(uri);

    reason = null;
    if (uri.Scheme == "voucha")
    {
      var hostPath = string.IsNullOrWhiteSpace(uri.Host) ? "" : "/" + uri.Host;
      var routePath = NativeRoutePattern.NormalizePath(hostPath + uri.AbsolutePath);
      pathAndQuery = routePath + uri.Query;
      return true;
    }

    pathAndQuery = "/";
    reason = "Unsupported deep-link scheme or host.";
    return false;
  }

}

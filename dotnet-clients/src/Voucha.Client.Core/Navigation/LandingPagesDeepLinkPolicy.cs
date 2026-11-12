namespace Voucha.Client.Core.Navigation;

public static class LandingPagesDeepLinkPolicy
{
  public static bool TryGetPublicTarget(NativeRouteMatch? match, out string? username, out string? slug)
  {
    username = null;
    slug = null;
    if (match?.Template is not ("/landing/:idOrUsername" or "/landing/:idOrUsername/:slug" or
        "/user/:idOrUsername/landing" or "/@:username" or "/@:username/:slug")) return false;
    username = match.Param("username", "idOrUsername");
    slug = match.Param("slug");
    return !string.IsNullOrWhiteSpace(username);
  }

  public static bool TryGetOwnerManagementSlug(NativeDeepLinkResolution resolution, out string? slug)
  {
    ArgumentNullException.ThrowIfNull(resolution);

    switch (resolution.Match?.Template)
    {
      case "/my/landing-pages":
        slug = null;
        return true;
      case "/my/landing-page/:slug":
        slug = resolution.Match.Param("slug");
        return true;
      default:
        slug = null;
        return false;
    }
  }
}

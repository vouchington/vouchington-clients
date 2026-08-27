namespace Voucha.Client.Core.Navigation;

public static class LandingPagesDeepLinkPolicy
{
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

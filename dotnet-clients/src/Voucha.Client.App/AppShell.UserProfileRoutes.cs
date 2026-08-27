using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Profiles;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenUserAdminRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.DestinationId != NativeRouteDestinationId.UserAdmin ||
        resolution.Match?.Param("idOrUsername") is not { } idOrUsername)
    {
      return null;
    }

    var isAdministrator = viewerProvider.CurrentViewer.Roles.Contains("administrator", StringComparer.Ordinal);
    return Navigation.PushAsync(CreateUserAdminPage(idOrUsername, isAdministrator));
  }

  private IdentityVerificationAttemptGrantPage CreateUserAdminPage(string idOrUsername, bool isAdministrator)
  {
    var page = serviceProvider.GetRequiredService<IdentityVerificationAttemptGrantPage>();
    _ = page.ApplyRouteAsync(idOrUsername, isAdministrator);
    return page;
  }

  private Task? TryOpenUserProfileRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.DestinationId == NativeRouteDestinationId.ProfileSettings &&
        string.Equals(resolution.Match?.Path, "/my/profile", StringComparison.OrdinalIgnoreCase))
    {
      return Navigation.PushAsync(CreateProfilePage());
    }

    if (resolution.DestinationId != NativeRouteDestinationId.UserProfile ||
        resolution.Match?.Param("idOrUsername") is not { } idOrUsername ||
        !NativeUserProfileRoutePaths.IsUserProfilePath(resolution.Match.Path))
    {
      return null;
    }

    if (!NativeUserProfileScope.TryParse(resolution.Match, out var scope)) return null;
    return Navigation.PushAsync(CreateProfilePage(idOrUsername, scope));
  }

  private ProfilePage CreateProfilePage(
      string? idOrUsername = null,
      NativeUserProfileScope? scope = null) =>
      string.IsNullOrWhiteSpace(idOrUsername)
          ? serviceProvider.GetRequiredService<ProfilePage>()
          : ActivatorUtilities.CreateInstance<ProfilePage>(serviceProvider, idOrUsername, scope ?? NativeUserProfileScope.Overview);
}

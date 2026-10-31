using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private bool IsStaffViewer => sessionStore.Current.Identity?.Roles?.Any(role =>
      role is "administrator" or "moderator") == true;

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI session handlers must not throw.")]
  private async Task RefreshAfterSessionChangedAsync()
  {
    if (!sessionChangedSubscription.IsAttached) return;
    var isStaffViewer = IsStaffViewer;
    var gainedStaffAccess = isStaffViewer && !lastStaffViewer;
    lastStaffViewer = isStaffViewer;
    RefreshViewerState();
    if (!gainedStaffAccess || !hasLoaded) return;
    try
    {
      if (string.IsNullOrWhiteSpace(idOrUsername)) await viewModel.LoadOwnAsync().ConfigureAwait(true);
      else await viewModel.LoadPublicScopeAsync(idOrUsername, viewModel.ProfileScope).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}

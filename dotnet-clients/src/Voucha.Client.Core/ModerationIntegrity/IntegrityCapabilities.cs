using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed record IntegrityCapabilities(
    bool CanReviewFlags,
    bool CanResolveFlags,
    bool CanApplyPenalties,
    bool CanReviewPenalties,
    bool CanRevokePenalties)
{
  public static IntegrityCapabilities FromViewer(NavigationViewer viewer)
  {
    ArgumentNullException.ThrowIfNull(viewer);
    var administrator = viewer.IsAuthenticated &&
        viewer.Roles.Contains("administrator", StringComparer.Ordinal);
    return new(administrator, administrator, administrator, administrator, administrator);
  }
}

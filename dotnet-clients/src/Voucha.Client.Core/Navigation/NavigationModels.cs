using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Navigation;

public sealed record NavigationViewer(
    bool IsAuthenticated,
    IReadOnlyList<string> Roles,
    IReadOnlyDictionary<string, bool>? FeatureFlags = null,
    string? IdentityId = null)
{
  public static NavigationViewer Anonymous { get; } =
      new(false, Array.Empty<string>());

  public bool HasSameIdentityAccess(NavigationViewer other)
  {
    ArgumentNullException.ThrowIfNull(other);
    return IsAuthenticated == other.IsAuthenticated &&
        string.Equals(IdentityId, other.IdentityId, StringComparison.Ordinal) &&
        (Roles ?? []).Order(StringComparer.Ordinal)
            .SequenceEqual((other.Roles ?? []).Order(StringComparer.Ordinal), StringComparer.Ordinal);
  }
}

public sealed class NavigationViewerChangedEventArgs : EventArgs
{
  public NavigationViewerChangedEventArgs(NavigationViewer viewer) =>
      Viewer = viewer;

  public NavigationViewer Viewer { get; }
}

public interface INavigationViewerProvider
{
  event EventHandler<NavigationViewerChangedEventArgs>? ViewerChanged;

  NavigationViewer CurrentViewer { get; }
}

public sealed class MutableNavigationViewerProvider : INavigationViewerProvider
{
  public event EventHandler<NavigationViewerChangedEventArgs>? ViewerChanged;

  public NavigationViewer CurrentViewer { get; private set; } = NavigationViewer.Anonymous;

  public void SetViewer(NavigationViewer viewer)
  {
    ArgumentNullException.ThrowIfNull(viewer);

    if (CurrentViewer == viewer)
    {
      return;
    }

    CurrentViewer = viewer;
    ViewerChanged?.Invoke(this, new NavigationViewerChangedEventArgs(viewer));
  }
}

public sealed record NavItem(
    UiMessageKey LabelKey,
    string Href,
    string DataPw,
    bool ComingSoon = false,
    bool RequiresAuth = false,
    bool Exact = false);

public sealed record NavGroup(
    UiMessageKey LabelKey,
    string DataPw,
    IReadOnlyList<NavItem> Items,
    bool RequiresAuth = false,
    IReadOnlyList<string>? Roles = null);

public sealed record NavIntent(
    string Id,
    UiMessageKey LabelKey,
    string Icon,
    IReadOnlyList<NavGroup> Groups,
    bool RequiresAuth = false,
    IReadOnlyList<string>? Roles = null,
    string? FeatureFlag = null);

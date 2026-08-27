using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private static bool HasSameIdentityAccess(NavigationViewer previous, NavigationViewer next) =>
      previous.HasSameIdentityAccess(next);

  private void SyncFeatureFlagNavigation(NavigationViewer viewer)
  {
    var tabBar = Items.OfType<TabBar>().SingleOrDefault();
    if (tabBar is null)
    {
      RebuildNavigation(viewer);
      return;
    }

    var expected = BottomTabShellViewModel.Create(viewer, preferenceStore.Load()).Tabs;
    var expectedIds = expected.Select(tab => tab.Id).ToHashSet(StringComparer.Ordinal);
    foreach (var stale in tabBar.Items.Where(item => !expectedIds.Contains(item.Route)).ToArray())
    {
      tabBar.Items.Remove(stale);
    }

    for (var index = 0; index < expected.Count; index++)
    {
      var model = expected[index];
      var existing = tabBar.Items.FirstOrDefault(item => item.Route == model.Id);
      if (existing is null)
      {
        existing = BottomTabFactory.Create(model, () => CreateIntentPage(model));
        tabBar.Items.Insert(index, existing);
      }
      else
      {
        var currentIndex = tabBar.Items.IndexOf(existing);
        if (currentIndex != index)
        {
          tabBar.Items.Remove(existing);
          tabBar.Items.Insert(index, existing);
        }
      }
    }

    renderedViewer = viewer;
  }
}

using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class BottomTabPreferencesTests
{
  [Fact]
  public void CreateAppliesStoredOrderAndHiddenTabs()
  {
    var preferences = new BottomTabPreferences(
        ["posts", "news", "web-search"],
        ["podcasts"]);

    var shell = BottomTabShellViewModel.Create(NavigationViewer.Anonymous, preferences);

    Assert.Equal("posts", shell.Tabs[0].Id);
    Assert.Equal("news", shell.Tabs[1].Id);
    Assert.Equal("web-search", shell.Tabs[2].Id);
    Assert.DoesNotContain(shell.Tabs, tab => tab.Id == "podcasts");
    Assert.Contains(shell.Tabs, tab => tab.Id == "communities");
  }

  [Fact]
  public void ApplyKeepsOneVisibleTabWhenEverythingIsHidden()
  {
    var visibleTabs = NavigationCatalog.GetVisibleBottomTabs(NavigationViewer.Anonymous);
    var preferences = new BottomTabPreferences(
        visibleTabs.Select(tab => tab.Id).ToArray(),
        visibleTabs.Select(tab => tab.Id).ToArray());

    var tabs = preferences.Apply(visibleTabs);

    var tab = Assert.Single(tabs);
    Assert.Equal("news", tab.Id);
  }

  [Fact]
  public void NormalizeDropsUnknownIdsAndAppendsNewVisibleTabs()
  {
    var visibleTabs = NavigationCatalog.GetVisibleBottomTabs(NavigationViewer.Anonymous);
    var preferences = new BottomTabPreferences(
        ["unknown", "posts"],
        ["missing", "news"]);

    var normalized = preferences.Normalize(visibleTabs);

    Assert.DoesNotContain("unknown", normalized.OrderedIntentIds);
    Assert.DoesNotContain("missing", normalized.HiddenIntentIds);
    Assert.Equal("posts", normalized.OrderedIntentIds[0]);
    Assert.Contains("communities", normalized.OrderedIntentIds);
    Assert.Contains("news", normalized.HiddenIntentIds);
  }
}

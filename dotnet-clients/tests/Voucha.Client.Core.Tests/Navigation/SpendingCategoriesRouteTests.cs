using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class SpendingCategoriesRouteTests
{
  [Fact]
  public void MapsToTheDedicatedAuthenticatedSettingsDestination()
  {
    var signedIn = NativeDeepLinkResolver.Resolve("voucha://my/spending-categories", new NavigationViewer(true, []));
    var signedOut = NativeDeepLinkResolver.Resolve("voucha://my/spending-categories", NavigationViewer.Anonymous);
    Assert.Equal(NativeRouteDestinationId.SpendingCategories, signedIn.DestinationId);
    Assert.Equal(NavigationCatalog.SettingsIntentId, signedIn.IntentId);
    Assert.True(signedOut.ShouldQueueUntilAuthenticated);
    Assert.Null(NativeRouteCatalog.Entries.Single(entry => entry.DestinationId == NativeRouteDestinationId.ProfileSettings).Match("/my/spending-categories"));
  }
}

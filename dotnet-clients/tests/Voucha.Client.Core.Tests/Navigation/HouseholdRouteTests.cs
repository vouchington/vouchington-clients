using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class HouseholdRouteTests
{
  [Fact]
  public void HouseholdHasAnExactAuthenticatedNativeDestination()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://my/household", new NavigationViewer(true, []));

    Assert.Equal(NativeDeepLinkStatus.Included, resolution.Status);
    Assert.Equal(NativeRouteDestinationId.Household, resolution.DestinationId);
    Assert.Equal(NavigationCatalog.SettingsIntentId, resolution.IntentId);
    Assert.Equal("/my/household", resolution.Match?.Path);
    Assert.True(resolution.CanNavigate);
    var paymentCards = NativeDeepLinkResolver.Resolve("voucha://my/cards", new NavigationViewer(true, []));
    Assert.Equal(NativeRouteDestinationId.PaymentCards, paymentCards.DestinationId);
  }

  [Fact]
  public void SignedOutHouseholdRouteQueuesAndRestoresAsTheExactDestination()
  {
    var signedOut = NativeDeepLinkResolver.Resolve("voucha://my/household", NavigationViewer.Anonymous);
    var restored = NativeDeepLinkResolver.Resolve("voucha://my/household", new NavigationViewer(true, []));

    Assert.True(signedOut.ShouldQueueUntilAuthenticated);
    Assert.False(signedOut.CanNavigate);
    Assert.Equal(NativeRouteDestinationId.Household, signedOut.DestinationId);
    Assert.False(restored.ShouldQueueUntilAuthenticated);
    Assert.True(restored.CanNavigate);
    Assert.Equal(NativeRouteDestinationId.Household, restored.DestinationId);
  }

}

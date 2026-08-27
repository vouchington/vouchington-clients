using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class RewardsProgramStatusesRouteTests
{
  [Fact]
  public void MyRewardStatusesUsesDedicatedAuthenticatedDestination()
  {
    var resolved = NativeDeepLinkResolver.Resolve("/my/rewards-program-statuses", new NavigationViewer(true, []));
    Assert.Equal(NativeRouteDestinationId.RewardsProgramStatuses, resolved.DestinationId);
    Assert.Equal("settings", resolved.IntentId);
  }

  [Fact]
  public void SignedOutDeepLinkPreservesExactScopeAndQueryUntilAuthentication()
  {
    var resolved = NativeDeepLinkResolver.Resolve(
        "/my/rewards-program-statuses?source=notification&after=opaque", new NavigationViewer(false, []));
    Assert.Equal(NativeRouteDestinationId.RewardsProgramStatuses, resolved.DestinationId);
    Assert.True(resolved.RequiresAuthentication);
    Assert.True(resolved.ShouldQueueUntilAuthenticated);
    Assert.False(resolved.CanNavigate);
    Assert.Equal("/my/rewards-program-statuses?source=notification&after=opaque", resolved.PathAndQuery);
  }
}

using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class CopyrightNoticesRouteTests
{
  [Theory]
  [InlineData("voucha://copyright/notices")]
  [InlineData("voucha://copyright/notices/case-1")]
  public void CasesRequireAuthenticationAndHaveTheirOwnNativeDestination(string uri)
  {
    var signedIn = NativeDeepLinkResolver.Resolve(uri, new NavigationViewer(true, []));
    var signedOut = NativeDeepLinkResolver.Resolve(uri, NavigationViewer.Anonymous);
    Assert.Equal(NativeRouteDestinationId.CopyrightNotices, signedIn.DestinationId);
    Assert.Equal(NavigationCatalog.SettingsIntentId, signedIn.IntentId);
    Assert.True(signedIn.CanNavigate);
    Assert.Equal(NativeRouteDestinationId.CopyrightNotices, signedOut.DestinationId);
    Assert.True(signedOut.ShouldQueueUntilAuthenticated);
    Assert.False(signedOut.CanNavigate);
  }
}

using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NativeDeepLinkResolverCommunityTests
{
  [Theory]
  [InlineData("voucha://communities", NativeRouteDestinationId.CommunitiesBrowse)]
  [InlineData("voucha://communities/lists", NativeRouteDestinationId.CommunitiesBrowse)]
  [InlineData("voucha://communities/test-community", NativeRouteDestinationId.CommunityDetail)]
  [InlineData("voucha://communities/test-community/about", NativeRouteDestinationId.CommunityDetail)]
  [InlineData("voucha://communities/test-community/apply", NativeRouteDestinationId.CommunityAction)]
  [InlineData("voucha://communities/create", NativeRouteDestinationId.CommunityAction)]
  [InlineData("voucha://communities/invite/invite-1", NativeRouteDestinationId.CommunityAction)]
  public void ResolveMapsCommunityRoutesToTheCommunitiesIntent(string input, NativeRouteDestinationId destinationId)
  {
    var resolution = NativeDeepLinkResolver.Resolve(input, new NavigationViewer(true, []));

    Assert.Equal(NativeDeepLinkStatus.Included, resolution.Status);
    Assert.Equal(destinationId, resolution.DestinationId);
    Assert.Equal("communities", resolution.IntentId);
    Assert.True(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveQueuesCommunityActionRoutesForAnonymousViewers()
  {
    var apply = NativeDeepLinkResolver.Resolve("voucha://communities/test-community/apply", NavigationViewer.Anonymous);
    var create = NativeDeepLinkResolver.Resolve("voucha://communities/create", NavigationViewer.Anonymous);

    Assert.True(apply.RequiresAuthentication);
    Assert.True(apply.ShouldQueueUntilAuthenticated);
    Assert.False(apply.CanNavigate);
    Assert.True(create.RequiresAuthentication);
    Assert.True(create.ShouldQueueUntilAuthenticated);
    Assert.False(create.CanNavigate);
  }

  [Fact]
  public void ResolveKeepsCommunityBrowseAndDetailRoutesPublic()
  {
    var browse = NativeDeepLinkResolver.Resolve("voucha://communities", NavigationViewer.Anonymous);
    var detail = NativeDeepLinkResolver.Resolve("voucha://communities/test-community", NavigationViewer.Anonymous);

    Assert.False(browse.RequiresAuthentication);
    Assert.False(browse.ShouldQueueUntilAuthenticated);
    Assert.True(browse.CanNavigate);
    Assert.False(detail.RequiresAuthentication);
    Assert.False(detail.ShouldQueueUntilAuthenticated);
    Assert.True(detail.CanNavigate);
  }
}

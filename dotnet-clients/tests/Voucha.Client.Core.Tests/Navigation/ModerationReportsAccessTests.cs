using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class ModerationReportsAccessTests
{
  [Fact]
  public void ReportsDeepLinkAllowsAuthenticatedMembersButNotAnonymousUsers()
  {
    var member = NativeDeepLinkResolver.Resolve("voucha://reports", new NavigationViewer(true, []));
    var moderator = NativeDeepLinkResolver.Resolve(
        "voucha://reports", new NavigationViewer(true, ["moderator"]));
    var anonymous = NativeDeepLinkResolver.Resolve("voucha://reports", NavigationViewer.Anonymous);

    Assert.True(member.CanNavigate);
    Assert.True(moderator.CanNavigate);
    Assert.False(anonymous.CanNavigate);
    Assert.True(anonymous.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void ReportsNavigationDiscoveryRemainsAdministratorOnly()
  {
    var intent = NavigationCatalog.All.Single(item => item.Id == "moderation");
    var memberGroups = NavigationCatalog.GetVisibleGroups(intent, new NavigationViewer(true, []));
    var moderatorGroups = NavigationCatalog.GetVisibleGroups(
        intent, new NavigationViewer(true, ["moderator"]));
    var administratorGroups = NavigationCatalog.GetVisibleGroups(
        intent, new NavigationViewer(true, ["administrator"]));

    Assert.DoesNotContain(memberGroups.SelectMany(group => group.Items), item => item.Href == "/reports");
    Assert.DoesNotContain(moderatorGroups.SelectMany(group => group.Items), item => item.Href == "/reports");
    Assert.Contains(administratorGroups.SelectMany(group => group.Items), item => item.Href == "/reports");
    Assert.Contains(moderatorGroups.SelectMany(group => group.Items), item => item.Href == "/appeals");
    Assert.Contains(moderatorGroups.SelectMany(group => group.Items), item => item.Href == "/disputes");
  }

  [Theory]
  [InlineData("voucha://appeals", NativeRouteDestinationId.ModerationAppeals)]
  [InlineData("voucha://disputes", NativeRouteDestinationId.ModerationDisputes)]
  public void AppealsAndDisputesRemainAvailableToModerators(
      string url,
      NativeRouteDestinationId expectedDestination)
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        url, new NavigationViewer(true, ["moderator"]));

    Assert.Equal(expectedDestination, resolution.DestinationId);
    Assert.True(resolution.CanNavigate);
  }
}

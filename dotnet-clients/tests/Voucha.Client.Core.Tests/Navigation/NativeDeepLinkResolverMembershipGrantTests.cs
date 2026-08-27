using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NativeDeepLinkResolverMembershipGrantTests
{
  [Theory]
  [InlineData(true, "administrator", true)]
  [InlineData(true, "moderator", false)]
  [InlineData(false, "", false)]
  public void ResolveMembershipGrantsKeepsExistingAdministratorGate(bool authenticated, string role, bool canNavigate)
  {
    var roles = string.IsNullOrEmpty(role) ? Array.Empty<string>() : new[] { role };
    var resolution = NativeDeepLinkResolver.Resolve("voucha://memberships/grants", new NavigationViewer(authenticated, roles));
    Assert.Equal(NativeRouteDestinationId.MembershipGrants, resolution.DestinationId);
    Assert.Equal(canNavigate, resolution.CanNavigate);
  }
}

using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NativeDeepLinkResolverUserAdminTests
{
  [Theory]
  [InlineData(true, "administrator", true)]
  [InlineData(true, "moderator", false)]
  [InlineData(false, "", false)]
  public void ResolveUserAdminMatchesWebPageRoles(bool authenticated, string role, bool canNavigate)
  {
    var roles = string.IsNullOrEmpty(role) ? Array.Empty<string>() : new[] { role };
    var resolution = NativeDeepLinkResolver.Resolve("voucha://user/alice/admin", new NavigationViewer(authenticated, roles));
    Assert.Equal(NativeRouteDestinationId.UserAdmin, resolution.DestinationId);
    Assert.Equal(canNavigate, resolution.CanNavigate);
    Assert.Equal("friends", resolution.IntentId);
  }
}

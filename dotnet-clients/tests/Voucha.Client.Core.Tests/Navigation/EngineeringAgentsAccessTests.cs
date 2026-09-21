using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class EngineeringAgentsAccessTests
{
  [Theory]
  [InlineData(false, "")]
  [InlineData(true, "moderator")]
  [InlineData(true, "developer")]
  [InlineData(true, "investor")]
  public void AgentRoutesDenyEveryoneExceptAdministrators(bool signedIn, string role)
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "/agent/a%2Fb/conversation/c%3Ad", new NavigationViewer(signedIn, string.IsNullOrEmpty(role) ? [] : [role]));
    Assert.False(resolution.CanNavigate);
  }

  [Fact]
  public void AgentRoutesAllowAdministratorsAndPreserveEscapedSegments()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "/agent/a%2Fb/conversation/c%3Ad", new NavigationViewer(true, ["administrator"]));
    Assert.True(resolution.CanNavigate);
    Assert.Equal("a/b", resolution.Match?.Param("idOrSlug"));
    Assert.Equal("c:d", resolution.Match?.Param("conversationId"));
  }
}

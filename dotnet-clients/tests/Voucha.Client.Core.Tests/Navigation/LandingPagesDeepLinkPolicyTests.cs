using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class LandingPagesDeepLinkPolicyTests
{
  [Theory]
  [InlineData("voucha://my/landing-pages", true, null)]
  [InlineData("voucha://my/landing-page/travel", true, "travel")]
  [InlineData("voucha://landing/alice", false, null)]
  [InlineData("voucha://landing/alice/travel", false, null)]
  [InlineData("voucha://my/landing-page/travel/analytics", false, null)]
  public void TryGetOwnerManagementSlugRecognizesOnlyOwnerManagementRoutes(
      string input,
      bool expectedMatch,
      string? expectedSlug)
  {
    var resolution = NativeDeepLinkResolver.Resolve(input, new NavigationViewer(true, []));

    var matched = LandingPagesDeepLinkPolicy.TryGetOwnerManagementSlug(resolution, out var slug);

    Assert.Equal(expectedMatch, matched);
    Assert.Equal(expectedSlug, slug);
  }
}

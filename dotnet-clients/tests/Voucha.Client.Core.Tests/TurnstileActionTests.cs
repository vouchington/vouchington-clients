using Voucha.Client.Core;
using Xunit;

namespace Voucha.Client.Core.Tests;

public sealed class TurnstileActionTests
{
  [Theory]
  [InlineData("posts.create", "posts-create")]
  [InlineData("communities.create-post", "communities-create-post")]
  [InlineData("auth.email-address-tokens", "auth-email-address-tokens")]
  [InlineData("a.b.c", "a-b-c")]
  [InlineData("AZaz09_-", "AZaz09_-")]
  [InlineData("abcdefghijklmnopqrstuvwxyz123456", "abcdefghijklmnopqrstuvwxyz123456")]
  public void MapsEveryDotAndPreservesAllowedAscii(string routeAction, string expected) =>
      Assert.Equal(expected, TurnstileAction.FromRouteAction(routeAction));

  [Theory]
  [InlineData("")]
  [InlineData("abcdefghijklmnopqrstuvwxyz1234567")]
  [InlineData("posts/create")]
  [InlineData(" posts.create")]
  [InlineData("posts.create\n")]
  [InlineData("é.create")]
  [InlineData("posts.😀")]
  [InlineData("</script>")]
  public void RejectsActionsOutsideCloudflareContract(string routeAction) =>
      Assert.Throws<InvalidOperationException>(() => TurnstileAction.FromRouteAction(routeAction));
}

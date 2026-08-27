using Voucha.Client.Core.Api;
using Voucha.Client.Core.Users;
using Xunit;

namespace Voucha.Client.Core.Tests.Users;

public sealed class UsersBrowseAdminTargetTests
{
  [Fact]
  public void NonBlankUsernamePrefersUsernameSegment()
  {
    var user = new UserSearchResult("11111111-1111-1111-1111-111111111111", "alice");

    var target = UsersBrowseViewModel.AdminTarget(user);

    Assert.Equal("/user/alice/admin", target.Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void MissingUsernameFallsBackToId(string? username)
  {
    var user = new UserSearchResult("22222222-2222-2222-2222-222222222222", username);

    var target = UsersBrowseViewModel.AdminTarget(user);

    Assert.Equal("/user/22222222-2222-2222-2222-222222222222/admin", target.Value);
  }

  [Fact]
  public void SegmentRequiringEscapingIsEscapedExactlyOnce()
  {
    var user = new UserSearchResult("33333333-3333-3333-3333-333333333333", "jane doe");

    var target = UsersBrowseViewModel.AdminTarget(user);

    Assert.Equal("/user/jane%20doe/admin", target.Value);
    Assert.DoesNotContain("%25", target.Value, StringComparison.Ordinal);
  }
}

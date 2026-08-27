using System.Net;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchUserFollowersAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("swift.users.followers.default");

    var response = await client.FetchUserFollowersAsync(
        new FetchUserFollowersRequest("user-abc"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users/user-abc/users/followers?limit=100");
    Assert.Equal("friend", response.Results[0].Username);
  }

  [Fact]
  public async Task FetchUserFollowingAsyncSendsPaginationCursor()
  {
    var (client, handler) = CreateClient("swift.users.following.default");

    await client.FetchUserFollowingAsync(
        new FetchUserFollowingRequest("user-abc", After: "cursor-1"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users/user-abc/users/following?after=cursor-1&limit=100");
  }
}

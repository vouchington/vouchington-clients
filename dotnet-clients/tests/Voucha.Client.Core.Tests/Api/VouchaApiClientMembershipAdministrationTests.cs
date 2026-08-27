using System.Net;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task GrantMembershipAsyncPostsTypedGrantBody()
  {
    var (client, handler) = CreateClient("native.memberships.grant.default");
    var response = await client.GrantMembershipAsync(
        new GrantMembershipBody("00000000-0000-7000-8000-000000000003", MembershipGrantPlanSlug.Plus, "00000000-0000-7000-8000-000000000701"),
        TestContext.Current.CancellationToken);
    AssertRequest(handler, HttpMethod.Post, "/api/v1/memberships");
    Assert.Equal("00000000-0000-7000-8000-000000000801", response.Membership.Id);
  }
}

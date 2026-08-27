using System.Net;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task GrantIdentityVerificationAttemptAsyncPostsTypedNote()
  {
    var (client, handler) = CreateClient("native.identity-verification-attempts.grant.default");
    var response = await client.GrantIdentityVerificationAttemptAsync(
        "00000000-0000-7000-8000-000000000003",
        new GrantIdentityVerificationAttemptBody("Provider terminal error reviewed by support."),
        TestContext.Current.CancellationToken);

    AssertRequest(
        handler,
        HttpMethod.Post,
        "/api/v1/admin/users/00000000-0000-7000-8000-000000000003/identity-verification-attempts");
    Assert.True(response.Granted);
  }
}

using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class AppleAuthEndpointParityTests
{
  [Fact]
  public void AppleSignInBodySerializesTheUserDataEnvelope()
  {
    var json = JsonSerializer.Serialize(
        new AppleSignInBody("token", "nonce", "Alice"),
        VouchaApiJson.Options);

    Assert.Equal("""{"token":"token","nonce":"nonce","userData":{"name":"Alice"}}""", json);
  }
}

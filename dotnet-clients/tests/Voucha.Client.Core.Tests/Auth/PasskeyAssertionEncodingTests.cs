using System.Text.Json;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class PasskeyAssertionEncodingTests
{
  [Theory]
  [InlineData("required")]
  [InlineData("discouraged")]
  [InlineData(null)]
  public void BothAuthenticatorRequestsRetainServerChallengeRpVerificationAndEveryOwnedId(string? verification)
  {
    var requests = PasskeyAssertionRequestPlan.Create(new("-_8", "voucha.test", verification,
        [new("AQI"), new("AwQ")]));
    Assert.Equal([PasskeyAuthenticatorKind.Platform, PasskeyAuthenticatorKind.SecurityKey], requests.Select(r => r.Kind));
    foreach (var request in requests)
    {
      Assert.Equal("voucha.test", request.RpId);
      Assert.Equal(verification, request.UserVerification);
      Assert.Equal(new byte[] { 251, 255 }, request.Challenge);
      Assert.Collection(request.AllowedCredentialIds,
          id => Assert.Equal(new byte[] { 1, 2 }, id), id => Assert.Equal(new byte[] { 3, 4 }, id));
    }
  }

  [Fact]
  public void DiscoverableCredentialsRemainAvailableForBothAuthenticators()
  {
    Assert.All(PasskeyAssertionRequestPlan.Create(new("AQ", "voucha.test", null, null)),
        request => Assert.Empty(request.AllowedCredentialIds));
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void PlatformAndSecurityKeyBytesUseTheSameWebAuthnWireEncoding(bool hasUserHandle)
  {
    var response = PasskeyAssertionEncoding.Create([251, 255], [1, 2], [3, 4], [5, 6],
        hasUserHandle ? [7, 8] : null);
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
    var root = json.RootElement;
    Assert.Equal("-_8", root.GetProperty("id").GetString());
    Assert.Equal("-_8", root.GetProperty("rawId").GetString());
    Assert.Equal("public-key", root.GetProperty("type").GetString());
    var assertion = root.GetProperty("response");
    Assert.Equal("AQI", assertion.GetProperty("authenticatorData").GetString());
    Assert.Equal("AwQ", assertion.GetProperty("clientDataJSON").GetString());
    Assert.Equal("BQY", assertion.GetProperty("signature").GetString());
    Assert.Equal(hasUserHandle ? "Bwg" : null, assertion.GetProperty("userHandle").GetString());
  }

  [Fact]
  public void MalformedServerCredentialEncodingFailsBeforeStartingNativeAuthorization()
  {
    Assert.Throws<FormatException>(() => PasskeyAssertionRequestPlan.Create(new("AQ", "voucha.test", null, [new("!")])));
    Assert.Throws<ArgumentNullException>(() => PasskeyAssertionRequestPlan.Create(null!));
    Assert.Throws<ArgumentNullException>(() => PasskeyAssertionEncoding.Decode(null!));
  }
}

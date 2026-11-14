using System.Text.Json;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class PasskeyAssertionRequestTests
{
  private static readonly Uri Origin = new("https://voucha.ai/sign-in?ignored=true");
  private static PasskeyAuthenticationOptions Options(string challenge = "-_8", string rp = "voucha.ai") =>
      new(challenge, rp, "preferred", [new("AQID")]);

  [Fact]
  public void RequestBindsChallengeToTrustedOriginAndPreservesCredentialBytes()
  {
    var request = PasskeyAssertionRequest.Create(Options(), Origin);
    using var json = JsonDocument.Parse(request.ClientDataJson);
    Assert.Equal("webauthn.get", json.RootElement.GetProperty("type").GetString());
    Assert.Equal("-_8", json.RootElement.GetProperty("challenge").GetString());
    Assert.Equal("https://voucha.ai", json.RootElement.GetProperty("origin").GetString());
    Assert.False(json.RootElement.GetProperty("crossOrigin").GetBoolean());
    Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(request.CredentialIds).ToArray());
    Assert.Equal("voucha.ai", request.RpId);
  }

  [Theory]
  [InlineData("https://attacker.example", "voucha.ai")]
  [InlineData("https://voucha.ai.attacker.example", "voucha.ai")]
  [InlineData("https://voucha.ai", "attacker.example")]
  [InlineData("https://user@voucha.ai", "voucha.ai")]
  [InlineData("http://voucha.ai", "voucha.ai")]
  [InlineData("/relative", "voucha.ai")]
  public void UntrustedOriginOrRpCannotReachAuthenticator(string origin, string rp) =>
      Assert.Throws<InvalidOperationException>(() => PasskeyAssertionRequest.Create(
          Options(rp: rp), new Uri(origin, UriKind.RelativeOrAbsolute)));

  [Theory]
  [InlineData("!")]
  [InlineData("AQ==")]
  [InlineData("A")]
  [InlineData("AQ ID")]
  public void MalformedChallengeIsRejected(string challenge) =>
      Assert.Throws<FormatException>(() => PasskeyAssertionRequest.Create(Options(challenge), Origin));

  [Fact]
  public void EmptyChallengeAndEmptyCredentialAreRejected()
  {
    Assert.Throws<InvalidOperationException>(() => PasskeyAssertionRequest.Create(Options(""), Origin));
    Assert.Throws<InvalidOperationException>(() => PasskeyAssertionRequest.Create(
        Options() with { AllowCredentials = [new("")] }, Origin));
  }

  [Fact]
  public void UnknownVerificationPreferenceIsRejected() =>
      Assert.Throws<InvalidOperationException>(() => PasskeyAssertionRequest.Create(
          Options() with { UserVerification = "invalid" }, Origin));

  [Fact]
  public void DiscoverableCredentialsWorkWithoutAllowlist()
  {
    var request = PasskeyAssertionRequest.Create(Options() with { AllowCredentials = null }, Origin);
    Assert.Empty(request.CredentialIds);
    Assert.Equal("_w", request.Complete([255], [1], [2], []).Id);
  }

  [Fact]
  public void AssertionUsesAuthenticatorDataAndOriginalClientDataInServerJson()
  {
    var request = PasskeyAssertionRequest.Create(Options(), Origin);
    var response = request.Complete([1, 2, 3], [255, 254], [251, 255], [250]);
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
    Assert.Equal("AQID", json.RootElement.GetProperty("id").GetString());
    Assert.Equal("AQID", json.RootElement.GetProperty("rawId").GetString());
    Assert.Equal("public-key", json.RootElement.GetProperty("type").GetString());
    Assert.Equal("{}", json.RootElement.GetProperty("clientExtensionResults").GetRawText());
    var assertion = json.RootElement.GetProperty("response");
    Assert.Equal("__4", assertion.GetProperty("authenticatorData").GetString());
    Assert.Equal("-_8", assertion.GetProperty("signature").GetString());
    Assert.Equal("-g", assertion.GetProperty("userHandle").GetString());
    Assert.Equal(request.ClientDataJson.ToArray(), Convert.FromBase64String(
        response.Response.ClientDataJSON.Replace('-', '+').Replace('_', '/').PadRight(
            (response.Response.ClientDataJSON.Length + 3) / 4 * 4, '=')));
    Assert.False(assertion.TryGetProperty("attestationObject", out _));
  }

  [Fact]
  public void MissingFieldsOrCredentialOutsideAllowlistAreRejected()
  {
    var request = PasskeyAssertionRequest.Create(Options(), Origin);
    Assert.Throws<InvalidOperationException>(() => request.Complete([], [1], [2], []));
    Assert.Throws<InvalidOperationException>(() => request.Complete([1, 2, 3], [], [2], []));
    Assert.Throws<InvalidOperationException>(() => request.Complete([1, 2, 3], [1], [], []));
    Assert.Throws<InvalidOperationException>(() => request.Complete([4], [1], [2], []));
  }
}

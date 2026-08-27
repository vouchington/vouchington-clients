using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PublicKeyPinningPolicyTests
{
  [Fact]
  public void DefaultPolicyMarksVouchaApiHostsEligibleWithoutPins()
  {
    var policy = PublicKeyPinningPolicy.VouchaDefault;

    Assert.True(policy.IsEligibleHost("voucha.ai"));
    Assert.True(policy.IsEligibleHost("staging.voucha.ai"));
    Assert.False(policy.HasConfiguredPins);
    Assert.False(policy.RequiresPinning(new Uri("https://voucha.ai/api/v1/session")));
    Assert.False(policy.RequiresPinning(new Uri("http://localhost:2999/api/v1/session")));
    Assert.True(policy.ShouldAcceptCertificate("voucha.ai", null, SslPolicyErrors.None));
    Assert.False(policy.IsEligibleHost(""));
    Assert.Equal(PublicKeyPinValidationResult.NotPinned, policy.Validate("", "expected"));
    Assert.True(policy.ShouldAcceptCertificate("", null, SslPolicyErrors.None));
  }

  [Fact]
  public void PolicyAcceptsMatchingCurrentOrBackupPinAndRejectsMismatch()
  {
    using var certificate = CreateCertificate();
    var spkiHash = PublicKeyPinningPolicy.TryGetLeafSpkiSha256Base64(certificate)!;
    var policy = new PublicKeyPinningPolicy(
        ["voucha.ai"],
        [
          new PublicKeyPin("voucha.ai", "backup", "next"),
          new PublicKeyPin("voucha.ai", spkiHash, "current"),
        ]);

    Assert.True(policy.HasConfiguredPins);
    Assert.Equal(PublicKeyPinValidationResult.Accepted, policy.Validate("voucha.ai", spkiHash));
    Assert.Equal(PublicKeyPinValidationResult.Accepted, policy.Validate("voucha.ai", "backup"));
    Assert.Equal(PublicKeyPinValidationResult.Rejected, policy.Validate("voucha.ai", "wrong"));
    Assert.True(policy.ShouldAcceptCertificate("voucha.ai", certificate, SslPolicyErrors.None));
    Assert.False(policy.ShouldAcceptCertificate("voucha.ai", null, SslPolicyErrors.None));
    Assert.False(policy.ShouldAcceptCertificate("voucha.ai", certificate, SslPolicyErrors.RemoteCertificateChainErrors));
  }

  [Fact]
  public void PolicySkipsLocalhostAndCustomHosts()
  {
    var policy = new PublicKeyPinningPolicy(
        ["voucha.ai"],
        [new PublicKeyPin("voucha.ai", "expected", "current")]);

    Assert.False(policy.RequiresPinning(new Uri("http://localhost:2999")));
    Assert.False(policy.RequiresPinning(new Uri("https://api.test")));
    Assert.Equal(PublicKeyPinValidationResult.NotPinned, policy.Validate("api.test", "expected"));
  }

  private static X509Certificate2 CreateCertificate()
  {
    return X509CertificateLoader.LoadCertificate(Convert.FromBase64String("""
        MIIDCTCCAfGgAwIBAgIUWQ/5DO5IEFvAWH4Nb3yGLcOJoRgwDQYJKoZIhvcNAQELBQAwFDESMBAGA1UEAwwJdm91Y2hhLmFpMB4XDTI2MDcwODA0NTA0OFoXDTI2MDcwOTA0NTA0OFowFDESMBAGA1UEAwwJdm91Y2hhLmFpMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvXaS1prgTAiN6kH3d/xOE1Os/p0fTGxsOpt1/CsN6y5kzL4KUtS060l8TjwKvETMetriLdAFNZOSo+LEAZwP+qkmzvfhyJ/Qokvx5hWYgqaYgCsx5jlvobogk0HwnsmC2+Bw4IOKV1A3ah6A7ZN4T3iQD/e9PWGGlA8uw0Y6ewcl+Nq/OV+tv/2FnTgoeIP94g0e4KCs1uAOiuGOc4vz9cJaJkgLYlnfjfW02yR3cfEvwesxgq76g5/Yw+T9dXwjPJLIje4OAj84f4ipgRGRuhiyapFiZ965xXNwNCczIzg+mVgFHsotJXmDii7AJN4/PtZ8fAUkxZVsG+pq7ljfXwIDAQABo1MwUTAdBgNVHQ4EFgQUGcq+cTGopQRB/xLfNjmqqjMwnMUwHwYDVR0jBBgwFoAUGcq+cTGopQRB/xLfNjmqqjMwnMUwDwYDVR0TAQH/BAUwAwEB/zANBgkqhkiG9w0BAQsFAAOCAQEAbAksz9u6BT9YmtpNDhWCpd3+h9csd5nBjbKdn71M+mOe9yGgB0GXp+ECi4E7eWmOL4E6UVQPN00rJqSAg3z0T+s1i1ceIUkmEQ9tpXA9zJbwFORO/c4GnqRlTjQ5tyUz7QIZ0tOXE6WgIXkkE18+/3C1tRPG1uekaqbgzduvcuCTPqtzZ8Qlrh9Gba6OuGqnCYQv3Qq8gst3pruSbKU+7Yf5EQvDVzNZ4KGWvUI0LDgl6MDlmK2JEthCgwgQ10sefn+r/uHqDQaxn8weKt5+8gzNKywjIljVD3p2lZPlZUtbCpmXX78P3H7SQEFBrTj+Od98/LEhp5I8Cx5fuFbA0w==
        """));
  }
}

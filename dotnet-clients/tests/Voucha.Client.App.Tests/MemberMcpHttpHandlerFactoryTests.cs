using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Voucha.Client.App.Chat;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class MemberMcpHttpHandlerFactoryTests
{
  [Fact]
  public void FreshCookieFreeHandlersApplyTheConfiguredCertificatePolicy()
  {
    using var key = RSA.Create(2048);
    var request = new CertificateRequest("CN=voucha.ai", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    var pin = PublicKeyPinningPolicy.TryGetLeafSpkiSha256Base64(certificate)!;
    var policy = new PublicKeyPinningPolicy(["voucha.ai"], [new PublicKeyPin("voucha.ai", pin, "current")]);
    var factory = new MemberMcpHttpHandlerFactory(new AppConfig(new Uri("https://voucha.ai")), policy);
    using var first = factory.Create();
    using var second = factory.Create();

    Assert.NotSame(first, second);
    Assert.False(first.UseCookies);
    Assert.False(first.AllowAutoRedirect);
    Assert.Equal(X509RevocationMode.Online, first.SslOptions.CertificateRevocationCheckMode);
    var validate = Assert.IsType<RemoteCertificateValidationCallback>(first.SslOptions.RemoteCertificateValidationCallback);
    Assert.True(validate(new object(), certificate, null, SslPolicyErrors.None));
    Assert.False(validate(new object(), null, null, SslPolicyErrors.None));
    Assert.False(validate(new object(), certificate, null, SslPolicyErrors.RemoteCertificateChainErrors));
  }
}

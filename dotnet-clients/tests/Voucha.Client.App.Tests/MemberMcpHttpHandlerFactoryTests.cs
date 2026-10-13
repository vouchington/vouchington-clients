using System.Net.Security;
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
    var policy = new PublicKeyPinningPolicy(["voucha.ai"], [new PublicKeyPin("voucha.ai", "configured-pin", "current")]);
    var factory = new MemberMcpHttpHandlerFactory(new AppConfig(new Uri("https://voucha.ai")), policy);
    using var first = factory.Create();
    using var second = factory.Create();

    Assert.NotSame(first, second);
    Assert.False(first.UseCookies);
    Assert.False(first.AllowAutoRedirect);
    Assert.Equal(X509RevocationMode.Online, first.SslOptions.CertificateRevocationCheckMode);
    var validate = Assert.IsType<RemoteCertificateValidationCallback>(first.SslOptions.RemoteCertificateValidationCallback);
    Assert.False(validate(new object(), null, null, SslPolicyErrors.None));

    var developmentFactory = new MemberMcpHttpHandlerFactory(new AppConfig(new Uri("https://development.example")), policy);
    using var development = developmentFactory.Create();
    var validateDevelopment = Assert.IsType<RemoteCertificateValidationCallback>(development.SslOptions.RemoteCertificateValidationCallback);
    Assert.True(validateDevelopment(new object(), null, null, SslPolicyErrors.None));
    Assert.False(validateDevelopment(new object(), null, null, SslPolicyErrors.RemoteCertificateChainErrors));
  }
}

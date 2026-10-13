using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Chat;

/// <summary>Applies the app certificate policy without sharing its cookie-bearing transport.</summary>
public sealed class MemberMcpHttpHandlerFactory(AppConfig config, PublicKeyPinningPolicy pinningPolicy)
{
  public SocketsHttpHandler Create() => new()
  {
    UseCookies = false,
    AllowAutoRedirect = false,
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    SslOptions = new SslClientAuthenticationOptions
    {
      CertificateRevocationCheckMode = X509RevocationMode.Online,
      RemoteCertificateValidationCallback = (sender, certificate, _, errors) =>
      {
        var host = sender is SslStream sslStream && !string.IsNullOrWhiteSpace(sslStream.TargetHostName)
            ? sslStream.TargetHostName : config.ApiBaseUrl.Host;
        return pinningPolicy.ShouldAcceptCertificate(host, certificate, errors);
      },
    },
  };
}

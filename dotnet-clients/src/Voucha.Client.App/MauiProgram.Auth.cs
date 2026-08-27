using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddAuthFeatureServices(IServiceCollection services)
  {
    services.AddSingleton<AuthLinkPrefillStore>();
    services.AddSingleton<AppleSignInCallbackStore>();
    services.AddSingleton<IAppleSignInProvider, PlatformAppleSignInProvider>();
    services.AddSingleton<IPasskeyAssertionProvider, PlatformPasskeyAssertionProvider>();
    services.AddSingleton<AuthenticationService>();
    services.AddTransient<AuthViewModel>();
  }

  private static void AddAuthSessionServices(IServiceCollection services)
  {
    services.AddSingleton<CookieContainer>();
    services.AddSingleton(PublicKeyPinningPolicy.VouchaDefault);
    services.AddSingleton<ISessionCookiePersistence>(sp =>
        new MauiSecureSessionCookiePersistence(
            sp.GetRequiredService<AppConfig>().ApiBaseUrl,
            sp.GetRequiredService<ILogger<MauiSecureSessionCookiePersistence>>()));
    services.AddSingleton(sp => new SessionCookieJar(
        sp.GetRequiredService<CookieContainer>(),
        sp.GetRequiredService<AppConfig>().ApiBaseUrl,
        sp.GetRequiredService<ISessionCookiePersistence>()));
    services.AddSingleton(sp =>
    {
      var pinningPolicy = sp.GetRequiredService<PublicKeyPinningPolicy>();
      var apiBaseHost = sp.GetRequiredService<AppConfig>().ApiBaseUrl.Host;
      return new SocketsHttpHandler
      {
        CookieContainer = sp.GetRequiredService<CookieContainer>(),
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        SslOptions = new SslClientAuthenticationOptions
        {
          CertificateRevocationCheckMode = X509RevocationMode.Online,
          RemoteCertificateValidationCallback = (sender, certificate, _, errors) =>
          {
            var host = sender is SslStream sslStream && !string.IsNullOrWhiteSpace(sslStream.TargetHostName)
                ? sslStream.TargetHostName
                : apiBaseHost;
            return pinningPolicy.ShouldAcceptCertificate(host, certificate, errors);
          },
        },
        UseCookies = true,
      };
    });
    services.AddSingleton(_ => NativeClientMetadata.Current());
    services.AddSingleton(sp => new HttpClient(
        new ClientMetadataHandler(
            sp.GetRequiredService<ClientMetadata>(),
            sp.GetRequiredService<AppConfig>().ApiBaseUrl,
            sp.GetRequiredService<SessionCookieJar>(),
            new SessionCookiePersistenceHandler(
                sp.GetRequiredService<SessionCookieJar>(),
                sp.GetRequiredService<SocketsHttpHandler>())),
        disposeHandler: true)
    {
      BaseAddress = sp.GetRequiredService<AppConfig>().ApiBaseUrl,
    });
    services.AddSingleton<VouchaApiClient>();
    services.AddSingleton<CookieSessionStore>();
    services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<CookieSessionStore>());
#if DEBUG
    services.AddSingleton<IDevelopmentSessionStore>(sp => sp.GetRequiredService<CookieSessionStore>());
#endif
  }
}

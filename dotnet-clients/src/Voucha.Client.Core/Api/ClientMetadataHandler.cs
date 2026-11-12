using System.Net.Http.Json;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Api;

public sealed partial class ClientMetadataHandler(
    ClientMetadata metadata,
    Uri apiOrigin,
    SessionCookieJar cookieJar,
    HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{

  protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    if (request.RequestUri is null ||
        Uri.Compare(
            apiOrigin,
            request.RequestUri,
            UriComponents.SchemeAndServer,
            UriFormat.SafeUnescaped,
            StringComparison.OrdinalIgnoreCase) != 0)
    {
      return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
    var transition = request.Method != HttpMethod.Get && IsSessionTransition(request.RequestUri.AbsolutePath);
    request.Options.Set(AuthenticationTransition, transition && request.RequestUri.AbsolutePath != "/api/v1/auth/logout");
    if (transition &&
        !(request.RequestUri.AbsolutePath == "/api/v1/auth/logout" &&
          request.Options.TryGetValue(CoordinatedSignOut, out var locallyCleared) && locallyCleared))
    {
      if (request.RequestUri.AbsolutePath == "/api/v1/auth/logout")
      {
        var cookies = await cookieJar.ClearForSignOutAsync(cookieJar.BeginSessionTransition()).ConfigureAwait(false);
        if (cookies is null) throw new OperationCanceledException("Sign-out was superseded.");
        request.Headers.Remove("Cookie");
        if (!string.IsNullOrEmpty(cookies)) request.Headers.TryAddWithoutValidation("Cookie", cookies);
        request.Options.Set(CoordinatedSignOut, true);
      }
      else request.Options.Set(AuthenticationDispatch, cookieJar.BeginAuthentication());
    }
    ApplyMetadata(request);
    if (!IsSessionEndpoint(request.RequestUri) && request.RequestUri.AbsolutePath != "/api/v1/auth/logout")
      await EnsureSessionBootstrapAsync(request, cancellationToken).ConfigureAwait(false);
    return await SendWithRecoveryAsync(request, cancellationToken).ConfigureAwait(false);
  }

  private async Task EnsureSessionBootstrapAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    await cookieJar.RestoreAsync(cancellationToken).ConfigureAwait(false);
    if (!request.Options.TryGetValue(AuthenticationDispatch, out _))
      await cookieJar.WaitForRenewalAsync(cancellationToken).ConfigureAwait(false);
    if (cookieJar.HasDeviceCookie) return;
    var userAgent = ReadUserAgent(request);
    await cookieJar.RenewAsync((dt, st, renewalToken) => RotateAsync(dt, st, userAgent, renewalToken), bootstrap: true, cancellationToken).ConfigureAwait(false);
  }

  private async Task<NativeSessionRefreshResponse> RotateAsync(
      string? deviceToken, string? sessionToken, string[] userAgent, CancellationToken cancellationToken)
  {
    using var renewal = new HttpRequestMessage(HttpMethod.Patch, new Uri(apiOrigin, "/api/v1/session"))
    {
      Content = JsonContent.Create(deviceToken is null ? (object)new { } :
          new RefreshSessionBody(deviceToken, sessionToken!)),
    };
    if (userAgent.Length > 0) renewal.Headers.TryAddWithoutValidation("User-Agent", userAgent);
    renewal.Options.Set(CoordinatedRotation, true);
    ApplyMetadata(renewal);
    using var response = await base.SendAsync(renewal, cancellationToken).ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
      throw new VouchaApiException(response.StatusCode,
          await VouchaApiClient.ReadErrorBodyAsync(response.Content, cancellationToken).ConfigureAwait(false));
    return await response.Content.ReadFromJsonAsync<NativeSessionRefreshResponse>(VouchaApiJson.Options, cancellationToken)
        .ConfigureAwait(false) ?? throw new InvalidDataException("Session renewal response was empty.");
  }

  private static string[] ReadUserAgent(HttpRequestMessage request) =>
      request.Headers.TryGetValues("User-Agent", out var values) ? values.ToArray() : [];

  private void ApplyMetadata(HttpRequestMessage request)
  {
    Set(request, "x-voucha-client", "dotnet");
    Set(request, "x-voucha-platform", metadata.Platform == ClientPlatform.Windows ? "windows" : "macos");
    Set(request, "x-voucha-app-version", metadata.AppVersion);
    if (metadata.SdkVersion is not null) Set(request, "x-voucha-sdk-version", metadata.SdkVersion);
    else request.Headers.Remove("x-voucha-sdk-version");
  }

  private static bool IsSessionTransition(string path) => path is
      "/api/v1/auth/email-address/login" or "/api/v1/auth/native/email-address/login" or
      "/api/v1/auth/mfa/totp/verification" or "/api/v1/auth/passkeys/authentication/verify" or
      "/api/v1/auth/oauth/apple/continue" or "/api/v1/auth/logout" ||
      path.StartsWith("/api/v1/auth/oauth/authorizations/", StringComparison.Ordinal) &&
      path.EndsWith("/complete", StringComparison.Ordinal);

  private static bool IsSessionEndpoint(Uri requestUri) =>
      string.Equals(requestUri.AbsolutePath, "/api/v1/session", StringComparison.Ordinal);

  private static void Set(HttpRequestMessage request, string name, string value)
  {
    request.Headers.Remove(name);
    request.Headers.TryAddWithoutValidation(name, value);
  }

}

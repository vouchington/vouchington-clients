using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Api;

public sealed class ClientMetadataHandler(
    ClientMetadata metadata,
    Uri apiOrigin,
    SessionCookieJar cookieJar,
    HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
  private readonly SemaphoreSlim bootstrapLock = new(1, 1);

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
    ApplyMetadata(request);
    if (!IsSessionEndpoint(request.RequestUri))
      await EnsureSessionBootstrapAsync(cancellationToken).ConfigureAwait(false);
    return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
  }

  private async Task EnsureSessionBootstrapAsync(CancellationToken cancellationToken)
  {
    await cookieJar.RestoreAsync(cancellationToken).ConfigureAwait(false);
    if (cookieJar.HasDeviceCookie) return;
    await bootstrapLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (cookieJar.HasDeviceCookie) return;
      using var bootstrap = new HttpRequestMessage(HttpMethod.Patch, new Uri(apiOrigin, "/api/v1/session"))
      {
        Content = JsonContent.Create(new { }),
      };
      ApplyMetadata(bootstrap);
      using var response = await base.SendAsync(bootstrap, cancellationToken).ConfigureAwait(false);
      response.EnsureSuccessStatusCode();
      var payload = await response.Content
          .ReadFromJsonAsync<SessionBootstrapResponse>(cancellationToken)
          .ConfigureAwait(false) ?? throw new InvalidDataException("Session bootstrap response was empty.");
      await cookieJar
          .StoreSessionTokensAsync(
              payload.Session.DeviceToken,
              payload.Session.SessionToken,
              TimeSpan.FromSeconds(payload.Session.DeviceExpirationSeconds),
              TimeSpan.FromSeconds(payload.Session.SessionExpirationSeconds),
              cancellationToken)
          .ConfigureAwait(false);
    }
    finally
    {
      bootstrapLock.Release();
    }
  }

  private void ApplyMetadata(HttpRequestMessage request)
  {
    Set(request, "x-voucha-client", "dotnet");
    Set(request, "x-voucha-platform", metadata.Platform == ClientPlatform.Windows ? "windows" : "macos");
    Set(request, "x-voucha-app-version", metadata.AppVersion);
    if (metadata.SdkVersion is not null) Set(request, "x-voucha-sdk-version", metadata.SdkVersion);
    else request.Headers.Remove("x-voucha-sdk-version");
  }

  private static bool IsSessionEndpoint(Uri requestUri) =>
      string.Equals(requestUri.AbsolutePath, "/api/v1/session", StringComparison.Ordinal);

  protected override void Dispose(bool disposing)
  {
    if (disposing) bootstrapLock.Dispose();
    base.Dispose(disposing);
  }

  private static void Set(HttpRequestMessage request, string name, string value)
  {
    request.Headers.Remove(name);
    request.Headers.TryAddWithoutValidation(name, value);
  }

  private sealed record SessionBootstrapResponse(
      [property: JsonPropertyName("session")] SessionBootstrap Session);

  private sealed record SessionBootstrap(
      [property: JsonPropertyName("dt")] string DeviceToken,
      [property: JsonPropertyName("st")] string SessionToken,
      [property: JsonPropertyName("dte")] int DeviceExpirationSeconds,
      [property: JsonPropertyName("ste")] int SessionExpirationSeconds);
}

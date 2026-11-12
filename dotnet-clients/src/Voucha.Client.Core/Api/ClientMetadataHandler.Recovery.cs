using System.Net;

namespace Voucha.Client.Core.Api;

public sealed partial class ClientMetadataHandler
{
  internal static readonly HttpRequestOptionsKey<bool> CoordinatedRotation = new("Voucha.CoordinatedRotation");

  internal static readonly HttpRequestOptionsKey<bool> RepeatableJson = new("Voucha.RepeatableJson");

  private async Task<HttpResponseMessage> SendWithRecoveryAsync(
      HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var generation = cookieJar.Generation;
    var cookies = cookieJar.GetCookieHeader(request.RequestUri!);
    var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.StatusCode != HttpStatusCode.Unauthorized ||
        !request.Options.TryGetValue(RepeatableJson, out var repeatable) || !repeatable ||
        IsRecoveryExcluded(request.RequestUri!.AbsolutePath)) return response;
    HttpResponseMessage? retried = null;
    try
    {
      var body = await VouchaApiClient.ReadErrorBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
      response.Content.Dispose();
      response.Content = new StringContent(body ?? "");
      if (new VouchaApiException(response.StatusCode, body).ErrorCode != "AUTH_REQUIRED") return response;
      if (generation != cookieJar.Generation || !cookieJar.CanRecoverAuthentication) return response;
      await cookieJar.WaitForRenewalAsync(cancellationToken).ConfigureAwait(false);
      if (cookies == cookieJar.GetCookieHeader(request.RequestUri))
      {
        var userAgent = ReadUserAgent(request);
        await cookieJar.RenewAsync((dt, st) => RotateAsync(dt, st, userAgent), cancellationToken: cancellationToken).ConfigureAwait(false);
      }
      if (generation != cookieJar.Generation) return response;
      using var replay = new HttpRequestMessage(request.Method, request.RequestUri);
      foreach (var header in request.Headers)
        if (!string.Equals(header.Key, "Cookie", StringComparison.OrdinalIgnoreCase))
          replay.Headers.TryAddWithoutValidation(header.Key, header.Value);
      if (request.Content is not null)
      {
        replay.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken)
            .ConfigureAwait(false));
        foreach (var header in request.Content.Headers)
          replay.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
      }
      var revision = cookieJar.CredentialRevision;
      retried = await base.SendAsync(replay, cancellationToken).ConfigureAwait(false);
      if (retried.StatusCode == HttpStatusCode.Unauthorized)
      {
        var retryBody = await VouchaApiClient.ReadErrorBodyAsync(retried.Content, cancellationToken).ConfigureAwait(false);
        retried.Content.Dispose();
        retried.Content = new StringContent(retryBody ?? "");
        if (new VouchaApiException(retried.StatusCode, retryBody).ErrorCode is "AUTH_REQUIRED" or
            "SESSION_RELOGIN_REQUIRED" or "SESSION_REFRESH_REUSED" or
            "SESSION_USER_AGENT_MISMATCH" or "SESSION_JA4_MISMATCH")
          await cookieJar.ClearIfCurrentAsync(generation, revision).ConfigureAwait(false);
      }
      response.Dispose();
      return retried;
    }
    catch
    {
      retried?.Dispose();
      response.Dispose();
      throw;
    }
  }

  private static bool IsRecoveryExcluded(string path) =>
      path == "/api/v1/session" || path.StartsWith("/api/v1/auth", StringComparison.Ordinal) ||
      path.StartsWith("/api/v1/login", StringComparison.Ordinal) ||
      path.StartsWith("/api/v1/logout", StringComparison.Ordinal);
}

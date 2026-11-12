using System.Net;

namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar
{
  private SessionCookie? ReadCookie(string name)
  {
    var cookie = cookieContainer.GetCookies(apiBaseUrl)[name];
    return cookie is null || string.IsNullOrEmpty(cookie.Value)
        ? null
        : new SessionCookie(
            name,
            cookie.Value,
            cookie.Expires == DateTime.MinValue ? null : new DateTimeOffset(cookie.Expires.ToUniversalTime()),
            cookie.Secure);
  }

  private void SetCookie(string name, string value, DateTimeOffset? expiresUtc = null, bool? secure = null) =>
      cookieContainer.Add(apiBaseUrl, new Cookie(name, value, "/")
      {
        HttpOnly = true,
        Secure = secure ?? apiBaseUrl.Scheme == Uri.UriSchemeHttps,
        Expires = expiresUtc?.UtcDateTime ?? DateTime.MinValue,
      });

  private void ExpireCookie(string name) =>
      cookieContainer.Add(apiBaseUrl, new Cookie(name, "", "/") { Expired = true });

  private bool IsSameOrigin(Uri requestUri) =>
      string.Equals(requestUri.Scheme, apiBaseUrl.Scheme, StringComparison.OrdinalIgnoreCase) &&
      string.Equals(requestUri.Host, apiBaseUrl.Host, StringComparison.OrdinalIgnoreCase) &&
      requestUri.Port == apiBaseUrl.Port;

  private static bool IsKnownCookie(SessionCookie cookie) =>
      SessionCookieNames.Contains(cookie.Name, StringComparer.Ordinal) &&
      !string.IsNullOrEmpty(cookie.Value);

  private static bool HasDeviceSession(IReadOnlyList<SessionCookie> cookies) =>
      cookies.Any(cookie => cookie.Name == "dt" && !string.IsNullOrEmpty(cookie.Value) &&
          (cookie.ExpiresUtc is null || cookie.ExpiresUtc > DateTimeOffset.UtcNow));

}

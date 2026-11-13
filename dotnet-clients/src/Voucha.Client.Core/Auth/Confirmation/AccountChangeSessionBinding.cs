using System.Security.Cryptography;
using System.Text;

namespace Voucha.Client.Core.Auth;

public static class AccountChangeSessionBinding
{
  public static string? Capture(ISessionStore store, SessionCookieJar jar, Uri origin)
  {
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(jar);
    ArgumentNullException.ThrowIfNull(origin);
    var id = store.Current.Identity?.Id;
    var cookies = jar.CookieContainer.GetCookies(origin);
    var device = cookies["dt"]?.Value;
    var session = cookies["st"]?.Value;
    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(device) || string.IsNullOrWhiteSpace(session)) return null;
    var authority = $"{origin.Scheme.ToUpperInvariant()}://{origin.IdnHost.ToUpperInvariant()}:{origin.Port}";
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{authority}\n{id}\n{device}\n{session}")));
  }
}

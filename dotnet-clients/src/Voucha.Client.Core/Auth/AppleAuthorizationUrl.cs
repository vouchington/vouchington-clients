using System.Security.Cryptography;
using System.Text;

namespace Voucha.Client.Core.Auth;

public static class AppleAuthorizationUrl
{
  public static Uri Create(AppConfig config, string state, string rawNonce)
  {
    ArgumentNullException.ThrowIfNull(config);

    var callback = new Uri(config.WebBaseUrl ?? new Uri(AppConfig.DefaultWebBaseUrl), "/auth/callback/apple");
    var builder = new UriBuilder("https://appleid.apple.com/auth/authorize")
    {
      Query = string.Join("&", new[]
      {
        Query("client_id", config.AppleClientId ?? ""),
        Query("redirect_uri", callback.ToString()),
        Query("response_type", "code id_token"),
        Query("response_mode", "fragment"),
        Query("scope", "name email"),
        Query("state", state),
        Query("nonce", Sha256Hex(rawNonce)),
      }),
    };
    return builder.Uri;
  }

  private static string Query(string key, string value) =>
      $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";

#pragma warning disable CA1308
  private static string Sha256Hex(string value) =>
      Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
#pragma warning restore CA1308
}

using System.Security.Cryptography;

namespace Voucha.Client.Core.Agent;

public enum MemberMcpApp { Ios, Macos, Windows }

/// <summary>One system-browser authorization attempt; caller keeps the verifier until callback redemption.</summary>
public sealed class MemberMcpOAuthRequest
{
  public string State { get; }
  public string Verifier { get; }
  public Uri ClientId { get; }
  public Uri RedirectUri { get; }
  public Uri Resource { get; }
  public string IssuerIdentifier { get; }
  public Uri AuthorizationUri { get; }

  public MemberMcpOAuthRequest(
      Uri siteOrigin,
      MemberMcpApp app,
      string issuerIdentifier,
      Uri authorizationEndpoint,
      Uri redirectUri,
      string scope = "mcp.user:read")
  {
    ArgumentNullException.ThrowIfNull(siteOrigin);
    ArgumentException.ThrowIfNullOrWhiteSpace(issuerIdentifier);
    ArgumentNullException.ThrowIfNull(authorizationEndpoint);
    ArgumentNullException.ThrowIfNull(redirectUri);
    if (!Uri.TryCreate(issuerIdentifier, UriKind.Absolute, out var issuer) ||
        siteOrigin.Scheme != Uri.UriSchemeHttps || siteOrigin.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(siteOrigin.UserInfo) || !string.IsNullOrEmpty(siteOrigin.Query) ||
        !string.IsNullOrEmpty(siteOrigin.Fragment) ||
        issuer.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(issuer.UserInfo) ||
        !string.IsNullOrEmpty(issuer.Query) || !string.IsNullOrEmpty(issuer.Fragment) ||
        authorizationEndpoint.Scheme != Uri.UriSchemeHttps ||
        string.IsNullOrWhiteSpace(scope))
      throw new ArgumentException("Valid HTTPS OAuth metadata and scope are required.");

    var platform = app switch
    {
      MemberMcpApp.Ios => "ios",
      MemberMcpApp.Macos => "macos",
      MemberMcpApp.Windows => "windows",
      _ => throw new ArgumentOutOfRangeException(nameof(app))
    };
    ClientId = new Uri(siteOrigin, $"/api/v1/oauth/native-clients/{platform}");
    Resource = new Uri(siteOrigin, "/api/v1/mcp");
    var expectedRedirect = new Uri(siteOrigin, $"/oauth/native/{platform}/callback");
    if (app == MemberMcpApp.Windows)
    {
      if (redirectUri.Scheme != Uri.UriSchemeHttp ||
          (redirectUri.Host != "127.0.0.1" && redirectUri.Host != "[::1]" && redirectUri.Host != "::1") ||
          redirectUri.AbsolutePath != "/oauth/native/windows/callback" || redirectUri.Port <= 0 ||
          !string.IsNullOrEmpty(redirectUri.UserInfo) || !string.IsNullOrEmpty(redirectUri.Query) ||
          !string.IsNullOrEmpty(redirectUri.Fragment))
        throw new ArgumentException("A Windows loopback callback with an assigned port is required.", nameof(redirectUri));
    }
    else if (redirectUri != expectedRedirect)
      throw new ArgumentException("The Apple HTTPS callback must match the native client document.", nameof(redirectUri));

    RedirectUri = redirectUri;
    IssuerIdentifier = issuerIdentifier;
    State = Base64Url(RandomNumberGenerator.GetBytes(32));
    Verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
    var challenge = Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(Verifier)));
    var query = new Dictionary<string, string>
    {
      ["response_type"] = "code",
      ["client_id"] = ClientId.AbsoluteUri,
      ["redirect_uri"] = RedirectUri.AbsoluteUri,
      ["scope"] = scope,
      ["resource"] = Resource.AbsoluteUri,
      ["state"] = State,
      ["code_challenge"] = challenge,
      ["code_challenge_method"] = "S256"
    };
    var existing = authorizationEndpoint.Query.TrimStart('?');
    if (existing.Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => Uri.UnescapeDataString(part.Split('=', 2)[0]))
        .Any(query.ContainsKey))
      throw new ArgumentException("The authorization endpoint has conflicting OAuth parameters.", nameof(authorizationEndpoint));
    AuthorizationUri = new UriBuilder(authorizationEndpoint)
    {
      Query = (string.IsNullOrEmpty(existing) ? "" : existing + "&") + string.Join("&", query.Select(pair =>
          $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))
    }.Uri;
  }

  public string CodeFromCallback(Uri callback)
  {
    ArgumentNullException.ThrowIfNull(callback);
    if (callback.Scheme != RedirectUri.Scheme || callback.Host != RedirectUri.Host ||
        callback.Port != RedirectUri.Port || callback.AbsolutePath != RedirectUri.AbsolutePath ||
        !string.IsNullOrEmpty(callback.UserInfo) || !string.IsNullOrEmpty(callback.Fragment))
      throw new InvalidDataException("OAuth callback redirect does not match the pending request.");
    var pairs = callback.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(value => value.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .Select(parts => (Key: Uri.UnescapeDataString(parts[0]), Value: Uri.UnescapeDataString(parts[1])))
        .ToArray();
    if (pairs.GroupBy(pair => pair.Key).Any(group => group.Count() > 1))
      throw new InvalidDataException("OAuth callback has duplicate security parameters.");
    var values = pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
    if (!values.TryGetValue("state", out var state) || state != State)
      throw new InvalidDataException("OAuth state does not match the pending request.");
    if (!values.TryGetValue("iss", out var issuerValue) || issuerValue != IssuerIdentifier)
      throw new InvalidDataException("OAuth issuer does not match discovery.");
    if (values.ContainsKey("code") == values.ContainsKey("error"))
      throw new InvalidDataException("OAuth callback has no single authorization outcome.");
    if (values.TryGetValue("error", out var error))
      throw new InvalidDataException($"OAuth authorization was denied: {error}");
    if (!values.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
      throw new InvalidDataException("OAuth callback has no code.");
    return code;
  }

  private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes)
      .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

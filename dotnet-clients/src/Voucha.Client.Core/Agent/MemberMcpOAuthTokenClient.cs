using System.Net;
using System.Net.Http.Json;

namespace Voucha.Client.Core.Agent;

public sealed class MemberMcpOAuthTokenClient : IDisposable
{
  private readonly HttpClient client;
  private readonly Uri endpoint;
  private readonly Uri revocationEndpoint;
  private readonly Uri clientId;
  private readonly Uri resource;

  internal MemberMcpOAuthTokenScope ScopeFor(string accountId) =>
      new(accountId, issuerIdentifier, resource, clientId);
  private readonly string issuerIdentifier;

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "HttpClient owns and disposes the handler.")]
  public MemberMcpOAuthTokenClient(
      MemberMcpOAuthMetadata metadata,
      Uri clientId,
      Uri resource,
      HttpMessageHandler? handler = null)
  {
    ArgumentNullException.ThrowIfNull(metadata);
    ArgumentNullException.ThrowIfNull(clientId);
    ArgumentNullException.ThrowIfNull(resource);
    if (metadata.TokenEndpoint.Scheme != Uri.UriSchemeHttps ||
        metadata.RevocationEndpoint.Scheme != Uri.UriSchemeHttps ||
        clientId.Scheme != Uri.UriSchemeHttps || resource.Scheme != Uri.UriSchemeHttps)
      throw new ArgumentException("HTTPS OAuth endpoints and resource are required.");
    endpoint = metadata.TokenEndpoint;
    revocationEndpoint = metadata.RevocationEndpoint;
    this.clientId = clientId;
    this.resource = resource;
    issuerIdentifier = metadata.IssuerIdentifier;
    client = new HttpClient(handler ?? new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false });
  }

  public Task<MemberMcpOAuthTokens> RedeemAsync(
      string code,
      string verifier,
      Uri redirectUri,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(redirectUri);
    return ExchangeAsync(new Dictionary<string, string>
    {
      ["grant_type"] = "authorization_code",
      ["code"] = code,
      ["code_verifier"] = verifier,
      ["redirect_uri"] = redirectUri.AbsoluteUri
    }, cancellationToken);
  }

  public Task<MemberMcpOAuthTokens> RefreshAsync(
      string refreshToken, CancellationToken cancellationToken = default) =>
      ExchangeAsync(new Dictionary<string, string>
      {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = refreshToken
      }, cancellationToken);

  public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, revocationEndpoint)
    {
      Content = new FormUrlEncodedContent(new Dictionary<string, string>
      {
        ["client_id"] = clientId.AbsoluteUri,
        ["token"] = refreshToken
      })
    };
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.StatusCode != HttpStatusCode.OK)
      throw new HttpRequestException("MCP OAuth token revocation failed.", null, response.StatusCode);
  }

  private async Task<MemberMcpOAuthTokens> ExchangeAsync(
      Dictionary<string, string> form,
      CancellationToken cancellationToken)
  {
    form["client_id"] = clientId.AbsoluteUri;
    form["resource"] = resource.AbsoluteUri;
    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
      Content = new FormUrlEncodedContent(form)
    };
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.BadRequest)
    {
      var error = await response.Content.ReadFromJsonAsync<OAuthError>(cancellationToken).ConfigureAwait(false);
      if (error?.Error == "invalid_grant") throw new McpUnauthorizedException();
    }
    if (response.StatusCode != HttpStatusCode.OK)
      throw new HttpRequestException("MCP OAuth token grant failed.", null, response.StatusCode);
    var tokens = await response.Content.ReadFromJsonAsync<MemberMcpOAuthTokens>(cancellationToken).ConfigureAwait(false);
    if (tokens is null || tokens.TokenType != "Bearer" || tokens.ExpiresIn <= 0 ||
        string.IsNullOrEmpty(tokens.AccessToken) || string.IsNullOrEmpty(tokens.RefreshToken))
      throw new InvalidDataException("MCP OAuth token grant returned invalid tokens.");
    return tokens;
  }

  public void Dispose() => client.Dispose();

  private sealed record OAuthError([property: System.Text.Json.Serialization.JsonPropertyName("error")] string Error);
}

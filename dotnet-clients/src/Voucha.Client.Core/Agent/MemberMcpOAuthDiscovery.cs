using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Agent;

public sealed record MemberMcpOAuthMetadata(
    string IssuerIdentifier,
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    Uri RevocationEndpoint);

/// <summary>Discovers the member MCP resource and its authorization server without app cookies.</summary>
public sealed class MemberMcpOAuthDiscovery : IDisposable
{
  private sealed record ResourceDocument(
      [property: JsonPropertyName("resource")] Uri Resource,
      [property: JsonPropertyName("authorization_servers")] IReadOnlyList<string> AuthorizationServers);

  private sealed record ServerDocument(
      [property: JsonPropertyName("issuer")] string Issuer,
      [property: JsonPropertyName("authorization_endpoint")] Uri AuthorizationEndpoint,
      [property: JsonPropertyName("token_endpoint")] Uri TokenEndpoint,
      [property: JsonPropertyName("revocation_endpoint")] Uri RevocationEndpoint);

  private readonly Uri siteOrigin;
  private readonly HttpClient client;

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "HttpClient owns and disposes the handler.")]
  public MemberMcpOAuthDiscovery(Uri siteOrigin, HttpMessageHandler? handler = null)
  {
    ArgumentNullException.ThrowIfNull(siteOrigin);
    if (siteOrigin.Scheme != Uri.UriSchemeHttps || siteOrigin.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(siteOrigin.UserInfo) ||
        !string.IsNullOrEmpty(siteOrigin.Query) || !string.IsNullOrEmpty(siteOrigin.Fragment))
      throw new ArgumentException("A HTTPS site origin is required.", nameof(siteOrigin));
    this.siteOrigin = siteOrigin;
    client = new HttpClient(handler ?? new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false });
  }

  public async Task<MemberMcpOAuthMetadata> DiscoverAsync(CancellationToken cancellationToken = default)
  {
    var resource = new Uri(siteOrigin, "/api/v1/mcp");
    var metadataUrl = new Uri(siteOrigin, "/.well-known/oauth-protected-resource/api/v1/mcp");
    var protectedResource = await ReadAsync<ResourceDocument>(metadataUrl, cancellationToken).ConfigureAwait(false);
    if (protectedResource.Resource != resource || protectedResource.AuthorizationServers.Count != 1)
      throw new InvalidDataException("The member MCP protected resource metadata is invalid.");
    var issuerIdentifier = protectedResource.AuthorizationServers[0];
    if (!Uri.TryCreate(issuerIdentifier, UriKind.Absolute, out var issuer) || issuer.Scheme != Uri.UriSchemeHttps ||
        !string.IsNullOrEmpty(issuer.UserInfo) || !string.IsNullOrEmpty(issuer.Query) ||
        !string.IsNullOrEmpty(issuer.Fragment))
      throw new InvalidDataException("The MCP authorization issuer must use HTTPS.");
    var serverUrl = new UriBuilder(issuer)
    {
      Path = "/.well-known/oauth-authorization-server" + (issuer.AbsolutePath == "/" ? "" : issuer.AbsolutePath)
    }.Uri;
    var server = await ReadAsync<ServerDocument>(serverUrl, cancellationToken).ConfigureAwait(false);
    if (server.Issuer != issuerIdentifier || server.AuthorizationEndpoint.Scheme != Uri.UriSchemeHttps ||
        server.TokenEndpoint.Scheme != Uri.UriSchemeHttps ||
        server.RevocationEndpoint.Scheme != Uri.UriSchemeHttps)
      throw new InvalidDataException("The MCP authorization server metadata is invalid.");
    return new MemberMcpOAuthMetadata(
        server.Issuer, server.AuthorizationEndpoint, server.TokenEndpoint, server.RevocationEndpoint);
  }

  private async Task<T> ReadAsync<T>(Uri url, CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Accept.ParseAdd("application/json");
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.StatusCode != HttpStatusCode.OK)
      throw new HttpRequestException("MCP OAuth discovery failed.", null, response.StatusCode);
    var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
    return result ?? throw new InvalidDataException("MCP OAuth discovery returned an empty document.");
  }

  public void Dispose() => client.Dispose();
}

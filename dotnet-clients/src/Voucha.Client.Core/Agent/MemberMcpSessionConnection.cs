using System.Text.Json;

namespace Voucha.Client.Core.Agent;

/// <summary>One account's OAuth grant and MCP transport, with a system-browser authorization fallback.</summary>
public sealed partial class MemberMcpSessionConnection : IMemberMcpSessionConnection
{
  private readonly Uri siteOrigin;
  private readonly MemberMcpOAuthMetadata metadata;
  private readonly string accountId;
  private readonly IMemberMcpAccountTokenStore store;
  private readonly IMemberMcpSystemBrowser browser;
  private readonly MemberMcpOAuthTokenClient tokenClient;
  private readonly MemberMcpTokenManager tokens;
  private readonly MemberMcpClient client;
  private readonly MemberMcpAuthorizedClient authorized;
  private bool disconnected;

  public MemberMcpSessionConnection(
      Uri siteOrigin, string accountId, MemberMcpOAuthMetadata metadata,
      IMemberMcpAccountTokenStore store, IMemberMcpSystemBrowser browser,
      Func<HttpMessageHandler>? createHandler = null)
  {
    this.siteOrigin = siteOrigin;
    this.metadata = metadata;
    this.accountId = accountId;
    this.store = store;
    this.browser = browser;
    var resource = new Uri(siteOrigin, "/api/v1/mcp");
    var clientId = new Uri(siteOrigin, "/api/v1/oauth/native-clients/windows");
    tokenClient = new MemberMcpOAuthTokenClient(metadata, clientId, resource, createHandler?.Invoke());
    tokens = new MemberMcpTokenManager(accountId, store, tokenClient);
    client = new MemberMcpClient(siteOrigin, createHandler?.Invoke());
    authorized = new MemberMcpAuthorizedClient(client, tokens);
  }

  public async Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken)
  {
    try { return await ListToolsAsync(cancellationToken).ConfigureAwait(false); }
    catch (McpUnauthorizedException) { }

    return await AuthorizeAndListToolsAsync("mcp.user:read", cancellationToken).ConfigureAwait(false);
  }

  public async Task<JsonElement> AuthorizeAndListToolsAsync(string scope, CancellationToken cancellationToken)
  {
    var callback = MemberMcpLoopbackCallback.Start();
    await using var callbackLifetime = callback.ConfigureAwait(false);
    var request = new MemberMcpOAuthRequest(
        siteOrigin, MemberMcpApp.Windows, metadata.IssuerIdentifier,
        metadata.AuthorizationEndpoint, callback.RedirectUri, scope);
    await store.SavePendingAsync(accountId, request.State, request.Verifier, request.RedirectUri)
        .ConfigureAwait(false);
    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      await browser.OpenAsync(request.AuthorizationUri, cancellationToken).ConfigureAwait(false);
      var redirected = await callback.ReceiveAsync(
          request.State, request.IssuerIdentifier, cancellationToken).ConfigureAwait(false);
      var code = request.CodeFromCallback(redirected);
      try
      {
        await tokens.RedeemAsync(code, request.Verifier, request.RedirectUri, cancellationToken)
            .ConfigureAwait(false);
      }
      catch (McpUnauthorizedException error)
      {
        throw new McpAuthorizationDeniedException("OAuth authorization code was rejected.", error);
      }
    }
    finally { await store.ClearPendingAsync(accountId).ConfigureAwait(false); }
    return await ListToolsAsync(cancellationToken).ConfigureAwait(false);
  }

  public Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken) =>
      MemberMcpToolCatalog.ListAllAsync(
          (cursor, token) => authorized.ListToolsAsync(cursor, token), cancellationToken);

  public Task<McpToolResult> CallToolAsync(
      string name, JsonElement arguments, CancellationToken cancellationToken) =>
      authorized.CallToolAsync(name, arguments, cancellationToken);

  public async Task DisconnectAsync(CancellationToken cancellationToken)
  {
    if (disconnected) return;
    disconnected = true;
    try
    {
      try
      {
        await client.CloseAsync(await tokens.AccessTokenAsync(cancellationToken).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);
      }
      catch (McpUnauthorizedException) { }
    }
    finally { await tokens.SignOutAsync(cancellationToken).ConfigureAwait(false); }
  }

  public async ValueTask DisposeAsync()
  {
    await tokens.BeginLocalSignOutAsync().ConfigureAwait(false);
    client.Dispose();
    tokenClient.Dispose();
  }
}

using System.Text.Json;

namespace Voucha.Client.Core.Agent;

/// <summary>
/// One account's MCP transport authorized by a token minted from the native session. There is no
/// browser authorization, consent screen, or scope request; the server decides read or write access
/// by plan and the catalog is whatever the server returns.
/// </summary>
public sealed class MemberMcpSessionBoundConnection : IMemberMcpSessionConnection
{
  private readonly MemberMcpClient client;
  private readonly MemberMcpSessionTokenSource tokens;
  private bool disconnected;

  public MemberMcpSessionBoundConnection(MemberMcpClient client, MemberMcpSessionTokenSource tokens)
  {
    ArgumentNullException.ThrowIfNull(client);
    ArgumentNullException.ThrowIfNull(tokens);
    this.client = client;
    this.tokens = tokens;
  }

  public Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken) =>
      ListToolsAsync(cancellationToken);

  /// <summary>The requested scope is ignored: server plan policy owns read/write authority.</summary>
  public Task<JsonElement> AuthorizeAndListToolsAsync(string scope, CancellationToken cancellationToken) =>
      ListToolsAsync(cancellationToken);

  public Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken) =>
      MemberMcpToolCatalog.ListAllAsync(
          (cursor, token) => WithTokenAsync(access => client.ListToolsAsync(access, cursor, token), token),
          cancellationToken);

  public Task<McpToolResult> CallToolAsync(
      string name, JsonElement arguments, CancellationToken cancellationToken) =>
      WithTokenAsync(access => client.CallToolAsync(name, arguments, access, cancellationToken), cancellationToken);

  /// <summary>A 401 re-mints once; a second 401 surfaces as unauthorized.</summary>
  private async Task<T> WithTokenAsync<T>(Func<string, Task<T>> call, CancellationToken cancellationToken)
  {
    var access = await tokens.AccessTokenAsync(cancellationToken).ConfigureAwait(false);
    try { return await call(access).ConfigureAwait(false); }
    catch (McpUnauthorizedException)
    {
      var fresh = await tokens.RefreshIfCurrentAsync(access, cancellationToken).ConfigureAwait(false);
      return await call(fresh).ConfigureAwait(false);
    }
  }

  /// <summary>Closes the remote session with an already-minted token and never mints a new one.</summary>
  public async Task DisconnectAsync(CancellationToken cancellationToken)
  {
    if (disconnected) return;
    disconnected = true;
    var access = tokens.CurrentAccessToken();
    tokens.Clear();
    if (access is null) return;
    try { await client.CloseAsync(access, cancellationToken).ConfigureAwait(false); }
    catch (McpUnauthorizedException) { }
  }

  public ValueTask DisposeAsync()
  {
    tokens.Dispose();
    client.Dispose();
    return ValueTask.CompletedTask;
  }
}

using System.Text.Json;

namespace Voucha.Client.Core.Agent;

/// <summary>A 401 triggers one single-flight refresh; a second 401 requires browser authorization.</summary>
public sealed class MemberMcpAuthorizedClient(MemberMcpClient client, MemberMcpTokenManager tokens)
{
  public async Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken = default)
      => await ListToolsAsync(null, cancellationToken).ConfigureAwait(false);

  public async Task<JsonElement> ListToolsAsync(string? cursor, CancellationToken cancellationToken = default)
  {
    var access = await tokens.AccessTokenAsync().ConfigureAwait(false);
    try
    {
      return await client.ListToolsAsync(access, cursor, cancellationToken).ConfigureAwait(false);
    }
    catch (McpUnauthorizedException)
    {
      var refreshed = await tokens.RefreshIfCurrentAsync(access, cancellationToken).ConfigureAwait(false);
      try
      {
        return await client.ListToolsAsync(refreshed, cursor, cancellationToken).ConfigureAwait(false);
      }
      catch (McpUnauthorizedException)
      {
        await tokens.ClearAsync().ConfigureAwait(false);
        throw;
      }
    }
  }

  public async Task<McpToolResult> CallToolAsync(
      string name, JsonElement arguments, CancellationToken cancellationToken = default)
  {
    var access = await tokens.AccessTokenAsync().ConfigureAwait(false);
    try
    {
      return await client.CallToolAsync(name, arguments, access, cancellationToken).ConfigureAwait(false);
    }
    catch (McpUnauthorizedException)
    {
      var refreshed = await tokens.RefreshIfCurrentAsync(access, cancellationToken).ConfigureAwait(false);
      try
      {
        return await client.CallToolAsync(name, arguments, refreshed, cancellationToken).ConfigureAwait(false);
      }
      catch (McpUnauthorizedException)
      {
        await tokens.ClearAsync().ConfigureAwait(false);
        throw;
      }
    }
  }
}

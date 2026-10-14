using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpSession
{
  public async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
  {
    using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
    await operation.WaitAsync(wait.Token).ConfigureAwait(false);
    try
    {
      IMemberMcpSessionConnection connection;
      long revision;
      CancellationToken sessionToken;
      lock (gate)
      {
        ThrowIfDisposed();
        connection = active ?? throw new InvalidOperationException("MCP is not connected.");
        Tools = null;
        revision = ++generation;
        sessionToken = activeCancellation?.Token ?? throw new InvalidOperationException("MCP is not connected.");
      }
      Changed?.Invoke(this, EventArgs.Empty);
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(wait.Token, sessionToken);
      JsonElement tools;
      try
      {
        tools = await connection.ListToolsAsync(linked.Token).ConfigureAwait(false);
        _ = new MemberMcpToolBridge(tools);
      }
      catch (McpUnauthorizedException)
      {
        lock (gate)
        {
          if (generation == revision && ReferenceEquals(active, connection))
            _ = QueueCleanup(accountId);
        }
        throw;
      }
      catch (InvalidDataException)
      {
        lock (gate)
        {
          if (generation == revision && ReferenceEquals(active, connection))
            _ = QueueCleanup(accountId);
        }
        throw;
      }
      catch
      {
        var invalidated = false;
        lock (gate)
        {
          if (generation == revision && ReferenceEquals(active, connection))
          {
            generation++;
            invalidated = true;
          }
        }
        if (invalidated) Changed?.Invoke(this, EventArgs.Empty);
        throw;
      }
      lock (gate)
      {
        if (generation != revision || !ReferenceEquals(active, connection)) return;
        Tools = tools;
        generation++;
      }
      Changed?.Invoke(this, EventArgs.Empty);
    }
    finally { operation.Release(); }
  }
}

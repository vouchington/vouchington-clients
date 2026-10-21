using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpSession
{
  public Task RefreshToolsAsync(CancellationToken cancellationToken = default) => RefreshCatalogAsync(null, cancellationToken);

  public Task AuthorizeAsync(string scope, CancellationToken cancellationToken = default) =>
      RefreshCatalogAsync(scope, cancellationToken);

  private async Task RefreshCatalogAsync(string? scope, CancellationToken cancellationToken)
  {
    using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
    await operation.WaitAsync(wait.Token).ConfigureAwait(false);
    try
    {
      IMemberMcpSessionConnection connection;
      CancellationTokenSource? priorCancellation = null;
      long revision;
      CancellationToken sessionToken;
      lock (gate)
      {
        ThrowIfDisposed();
        connection = active ?? throw new InvalidOperationException("MCP is not connected.");
        Tools = null;
        if (scope is not null)
        {
          priorCancellation = activeCancellation;
          activeCancellation = new CancellationTokenSource();
          Status = MemberMcpSessionStatus.Connecting;
        }
        revision = ++generation;
        sessionToken = activeCancellation?.Token ?? throw new InvalidOperationException("MCP is not connected.");
      }
      if (priorCancellation is not null)
      {
        await priorCancellation.CancelAsync().ConfigureAwait(false);
        priorCancellation.Dispose();
      }
      Changed?.Invoke(this, EventArgs.Empty);
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(wait.Token, sessionToken);
      JsonElement tools;
      try
      {
        tools = scope is null
            ? await connection.ListToolsAsync(linked.Token).ConfigureAwait(false)
            : await connection.AuthorizeAndListToolsAsync(scope, linked.Token).ConfigureAwait(false);
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
        // Re-list using the current token, rather than restoring capabilities from the old grant.
        JsonElement? recovered = null;
        if (scope is not null && !sessionToken.IsCancellationRequested)
        {
          try
          {
            var current = await connection.ListToolsAsync(sessionToken).ConfigureAwait(false);
            _ = new MemberMcpToolBridge(current);
            recovered = current;
          }
          catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or McpUnauthorizedException) { }
        }
        var invalidated = false;
        lock (gate)
        {
          if (generation == revision && ReferenceEquals(active, connection))
          {
            Tools = recovered;
            generation++;
            Status = MemberMcpSessionStatus.Connected;
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
        Status = MemberMcpSessionStatus.Connected;
        generation++;
      }
      Changed?.Invoke(this, EventArgs.Empty);
    }
    finally { operation.Release(); }
  }
}

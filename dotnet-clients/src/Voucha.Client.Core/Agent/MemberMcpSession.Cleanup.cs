using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpSession
{
  private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
  {
    lock (gate)
    {
      if (disposed) return;
      var next = args.Snapshot.Identity?.Id;
      if (next == accountId) return;
      var prior = accountId;
      accountId = next;
      _ = QueueCleanup(prior);
    }
  }

  private Task QueueCleanup(string? priorAccount)
  {
    generation++;
    if (pending is not null) _ = pending.CancelAsync();
    if (activeCancellation is not null) _ = activeCancellation.CancelAsync();
    Tools = null;
    Status = MemberMcpSessionStatus.Disconnected;
    Changed?.Invoke(this, EventArgs.Empty);
    var previous = cleanupTail;
    cleanupTail = CleanupAfterAsync(previous, priorAccount);
    return cleanupTail;
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification =
      "Later account cleanup must run even when prior cleanup failed; all indexed grants are cleared before reconnecting.")]
  private async Task CleanupAfterAsync(Task previous, string? priorAccount)
  {
    var priorFailed = false;
    try { await previous.ConfigureAwait(false); }
    catch (Exception) { priorFailed = true; }
    await operation.WaitAsync().ConfigureAwait(false);
    try
    {
      IMemberMcpSessionConnection? connection;
      lock (gate)
      {
        connection = active;
        active = null;
        activeCancellation?.Dispose();
        activeCancellation = null;
      }
      try
      {
        if (connection is not null)
          await DisconnectBestEffortAsync(connection).ConfigureAwait(false);
        else if (priorAccount is not null &&
            await store.HasAccountGrantAsync(priorAccount).ConfigureAwait(false))
          await RevokePersistedGrantAsync(priorAccount).ConfigureAwait(false);
      }
      finally
      {
        if (priorAccount is not null) await store.ClearAccountAsync(priorAccount).ConfigureAwait(false);
      }
      if (priorFailed) await store.ClearAllAccountsAsync().ConfigureAwait(false);
    }
    finally { operation.Release(); }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification =
      "Remote close and revoke are best effort; protected exact-account cleanup remains mandatory.")]
  private static async Task DisconnectBestEffortAsync(IMemberMcpSessionConnection connection)
  {
    try { await connection.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); }
    catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP remote close failed: {error.GetType().Name}"); }
    try { await connection.DisposeAsync().ConfigureAwait(false); }
    catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP remote dispose failed: {error.GetType().Name}"); }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification =
      "Offline discovery or revocation cannot prevent exact-account local credential deletion.")]
  private async Task RevokePersistedGrantAsync(string account)
  {
    try
    {
      var connection = await factory.CreateAsync(account, CancellationToken.None).ConfigureAwait(false);
      await DisconnectBestEffortAsync(connection).ConfigureAwait(false);
    }
    catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP persisted grant revoke failed: {error.GetType().Name}"); }
  }

  private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

  public void Dispose()
  {
    Task completion;
    lock (gate)
    {
      if (disposed) return;
      disposed = true;
      sessionStore.SessionChanged -= OnSessionChanged;
      completion = QueueCleanup(accountId);
    }
    lifetime.Cancel();
    _ = completion.ContinueWith(settled =>
    {
      _ = settled.Exception;
      operation.Dispose();
      lifetime.Dispose();
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
  }
}

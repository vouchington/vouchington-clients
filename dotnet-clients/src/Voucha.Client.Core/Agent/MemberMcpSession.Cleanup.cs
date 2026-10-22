using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpSession
{
  /// <summary>Reconcile cold-start grants only after identity restoration has completed.</summary>
  public Task ConfirmStartupSessionAsync()
  {
    lock (gate)
    {
      ThrowIfDisposed();
      var previous = cleanupTail;
      cleanupTail = ClearStaleStartupAccountsAsync(previous);
      return cleanupTail;
    }
  }

  private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
  {
    lock (gate)
    {
      if (disposed) return;
      var next = args.Snapshot.Identity?.Id;
      if (next == accountId) return;
      var prior = accountId;
      RequestWriteAccess = false;
      accountId = next;
      accountEpoch++;
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
    var previous = cleanupTail;
    cleanupTail = CleanupAfterAsync(previous, priorAccount);
    Changed?.Invoke(this, EventArgs.Empty);
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
      var drain = connection is MemberMcpSessionConnection live ? live.BeginLocalSignOutAsync() : null;
      IReadOnlyList<MemberMcpStoredGrant> grants;
      try
      {
        grants = priorAccount is null ? [] :
            await store.TakeAccountAsync(priorAccount).ConfigureAwait(false);
        if (priorAccount is null && accountId is not null)
          await store.ClearAllAccountsExceptAsync(accountId).ConfigureAwait(false);
        if (drain is not null)
        {
          await drain.ConfigureAwait(false);
          grants = [.. grants, .. await store.TakeAccountAsync(priorAccount!).ConfigureAwait(false)];
        }
      }
      catch
      {
        if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
        throw;
      }
      if (connection is MemberMcpSessionConnection current)
        _ = current.StartRemoteCleanup(grants);
      else if (connection is not null)
        _ = DisconnectBestEffortAsync(connection);
      else if (priorAccount is not null && grants.Count > 0)
        _ = RevokePersistedGrantAsync(priorAccount, grants);
      if (priorFailed) await store.ClearAllAccountsAsync().ConfigureAwait(false);
    }
    finally { operation.Release(); }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification =
      "Confirmed startup must retry protected stale-account removal even after an earlier cleanup failed.")]
  private async Task ClearStaleStartupAccountsAsync(Task previous)
  {
    try { await previous.ConfigureAwait(false); }
    catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP earlier account cleanup failed: {error.GetType().Name}"); }
    await operation.WaitAsync().ConfigureAwait(false);
    try
    {
      string? retainedAccount;
      lock (gate) { retainedAccount = accountId; }
      await store.ClearAllAccountsExceptAsync(retainedAccount).ConfigureAwait(false);
    }
    finally { operation.Release(); }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification =
      "Remote close and revoke are best effort; protected exact-account cleanup remains mandatory.")]
  private static async Task DisconnectBestEffortAsync(IMemberMcpSessionConnection connection)
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try { await connection.DisconnectAsync(timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false); }
    catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP remote close failed: {error.GetType().Name}"); }
    try { await connection.DisposeAsync().ConfigureAwait(false); }
    catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP remote dispose failed: {error.GetType().Name}"); }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification =
      "Offline discovery or revocation cannot prevent exact-account local credential deletion.")]
  private async Task RevokePersistedGrantAsync(string account, IReadOnlyList<MemberMcpStoredGrant> grants)
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
      var connection = await factory.CreateAsync(account, timeout.Token).WaitAsync(timeout.Token)
          .ConfigureAwait(false);
      if (connection is MemberMcpSessionConnection current) _ = current.StartRemoteCleanup(grants);
      else await DisconnectBestEffortAsync(connection).ConfigureAwait(false);
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

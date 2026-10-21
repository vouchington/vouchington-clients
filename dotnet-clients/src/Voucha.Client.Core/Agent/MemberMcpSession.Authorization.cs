namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpSession
{
  public async Task AuthorizeAsync(string scope, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    IMemberMcpSessionConnection connection;
    CancellationTokenSource previous;
    CancellationToken authorizationToken;
    long admissionRevision;
    lock (gate)
    {
      ThrowIfDisposed();
      connection = active ?? throw new InvalidOperationException("MCP is not connected.");
      if (Status == MemberMcpSessionStatus.Connecting)
        throw new InvalidOperationException("MCP consent is already in progress.");
      previous = activeCancellation ?? throw new InvalidOperationException("MCP is not connected.");
      activeCancellation = new CancellationTokenSource();
      authorizationToken = activeCancellation.Token;
      Tools = null;
      Status = MemberMcpSessionStatus.Connecting;
      admissionRevision = ++generation;
    }
    Changed?.Invoke(this, EventArgs.Empty);
    await previous.CancelAsync().ConfigureAwait(false);
    previous.Dispose();
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken, lifetimeToken, authorizationToken);
    try { await RefreshCatalogAsync(scope, linked.Token).ConfigureAwait(false); }
    catch
    {
      bool recover;
      lock (gate) recover = generation == admissionRevision && ReferenceEquals(active, connection);
      if (recover && !authorizationToken.IsCancellationRequested)
      {
        // If cancellation happened before semaphore admission, recover through the current grant.
        try { await RefreshCatalogAsync(null, authorizationToken, recoveringConsent: true).ConfigureAwait(false); }
        catch (Exception error) when (error is not OutOfMemoryException) { }
      }
      throw;
    }
    finally
    {
      var settled = false;
      lock (gate)
      {
        if (generation == admissionRevision && ReferenceEquals(active, connection) &&
            Status == MemberMcpSessionStatus.Connecting)
        {
          Status = Tools is null ? MemberMcpSessionStatus.Error : MemberMcpSessionStatus.Connected;
          settled = true;
        }
      }
      if (settled) Changed?.Invoke(this, EventArgs.Empty);
    }
  }
}

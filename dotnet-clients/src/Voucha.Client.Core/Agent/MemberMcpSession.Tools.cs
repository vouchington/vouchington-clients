using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public sealed record MemberMcpConnectedSnapshot(
    string AccountId, long Revision, JsonElement Catalog, CancellationToken AccountCancellation);

public sealed partial class MemberMcpSession
{
  public MemberMcpConnectedSnapshot? ConnectedSnapshot()
  {
    lock (gate)
    {
      if (disposed || Status != MemberMcpSessionStatus.Connected ||
          active is null || accountId is null || Tools is not { } catalog ||
          sessionStore.Current.Identity?.Id != accountId) return null;
      return new MemberMcpConnectedSnapshot(accountId, generation, catalog.Clone(), activeCancellation!.Token);
    }
  }

  public bool IsCurrent(MemberMcpConnectedSnapshot snapshot)
  {
    ArgumentNullException.ThrowIfNull(snapshot);
    lock (gate) return IsCurrentLocked(snapshot);
  }

  public bool MatchesAccount(string candidateAccountId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(candidateAccountId);
    lock (gate)
      return !disposed && sessionStore.Current.Identity?.Id == candidateAccountId;
  }

  private bool IsCurrentLocked(MemberMcpConnectedSnapshot snapshot) =>
      !disposed && Status == MemberMcpSessionStatus.Connected && active is not null &&
      accountId == snapshot.AccountId && generation == snapshot.Revision &&
      sessionStore.Current.Identity?.Id == snapshot.AccountId;

  public async Task<McpToolResult> CallToolAsync(
      MemberMcpConnectedSnapshot snapshot, string name, JsonElement arguments,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(snapshot);
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
    await operation.WaitAsync(wait.Token).ConfigureAwait(false);
    try
    {
      IMemberMcpSessionConnection connection;
      CancellationToken sessionToken;
      lock (gate)
      {
        if (!IsCurrentLocked(snapshot)) throw new OperationCanceledException("MCP account or catalog changed.");
        connection = active!;
        sessionToken = activeCancellation!.Token;
      }
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(wait.Token, sessionToken);
      var result = await connection.CallToolAsync(name, arguments, linked.Token).ConfigureAwait(false);
      linked.Token.ThrowIfCancellationRequested();
      lock (gate)
      {
        if (!IsCurrentLocked(snapshot)) throw new OperationCanceledException("MCP account or catalog changed.");
      }
      return result;
    }
    finally { operation.Release(); }
  }
}

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpSessionConnection
{
  internal Task BeginLocalSignOutAsync() => tokens.BeginLocalSignOutAsync();

  /// <summary>The protected account document has already been removed before this starts.</summary>
  internal Task StartRemoteCleanup(IReadOnlyList<MemberMcpStoredGrant> grants)
  {
    var currentScope = tokenClient.ScopeFor(accountId);
    var current = grants.LastOrDefault(grant => grant.Scope == currentScope);
    return CompleteRemoteCleanupAsync(current is null ? [] : [current]);
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification =
      "Remote close/revoke is best effort after protected local deletion; each failure is diagnosed.")]
  private async Task CompleteRemoteCleanupAsync(IReadOnlyList<MemberMcpStoredGrant> grants)
  {
    using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var revokeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
      foreach (var grant in grants)
      {
        Task? close = null;
        if (grant.Tokens.AcquiredAt.AddSeconds(grant.Tokens.ExpiresIn - 30) > DateTimeOffset.UtcNow)
          close = client.CloseAsync(grant.Tokens.AccessToken, closeTimeout.Token);
        var revoke = tokenClient.RevokeAsync(grant.Tokens.RefreshToken, revokeTimeout.Token);
        try { await revoke.ConfigureAwait(false); }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP token revoke failed: {error.GetType().Name}"); }
        if (close is not null)
        {
          try { await close.ConfigureAwait(false); }
          catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP remote close failed: {error.GetType().Name}"); }
        }
      }
    }
    finally
    {
      try { await DisposeAsync().ConfigureAwait(false); }
      catch (Exception error) { System.Diagnostics.Debug.WriteLine($"MCP remote dispose failed: {error.GetType().Name}"); }
    }
  }
}

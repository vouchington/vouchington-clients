namespace Voucha.Client.Core.Agent;

/// <summary>Refresh-token rotation is single-flight for one signed-in member.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification =
    "A canceled network exchange may ignore cancellation and complete after sign-out; retain managed fencing primitives until that task is collected.")]
public sealed partial class MemberMcpTokenManager(
    string accountId,
    IMemberMcpOAuthTokenStore store,
    MemberMcpOAuthTokenClient tokenClient)
{
  private readonly object gate = new();
  private readonly SemaphoreSlim localMutation = new(1, 1);
  private readonly CancellationTokenSource exchangeCancellation = new();
  private Task mutationTail = Task.CompletedTask;
  private readonly MemberMcpOAuthTokenScope scope = tokenClient.ScopeFor(accountId);
  private Task<MemberMcpOAuthTokens>? refreshTask;
  private Task<MemberMcpOAuthTokens>? redeemTask;
  private Task? clearTask;
  private MemberMcpOAuthTokens? latestRefreshedTokens;
  private bool signedOut;
  private bool clearing;

  public async Task<string> AccessTokenAsync(CancellationToken cancellationToken = default)
  {
    lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
    var tokens = await store.LoadAsync(scope).ConfigureAwait(false)
        ?? throw new McpUnauthorizedException();
    lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
    if (tokens.AcquiredAt.AddSeconds(tokens.ExpiresIn - 30) <= DateTimeOffset.UtcNow)
      return await RefreshAsync(cancellationToken).ConfigureAwait(false);
    return tokens.AccessToken;
  }

  public async Task<string> RedeemAsync(
      string code, string verifier, Uri redirectUri, CancellationToken cancellationToken = default)
  {
    Task<MemberMcpOAuthTokens> pending;
    lock (gate)
    {
      if (signedOut || clearing || redeemTask is not null) throw new McpUnauthorizedException();
      redeemTask = RedeemCoreAsync(code, verifier, redirectUri);
      pending = redeemTask;
      ObserveCompletion(pending, refresh: false);
    }
    var tokens = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
    lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
    return tokens.AccessToken;
  }

  private async Task<MemberMcpOAuthTokens> RedeemCoreAsync(string code, string verifier, Uri redirectUri)
  {
    return await SerializeMutationAsync(async () =>
    {
      var tokens = await tokenClient.RedeemAsync(code, verifier, redirectUri, exchangeCancellation.Token)
          .ConfigureAwait(false);
      await SaveActiveTokensAsync(tokens).ConfigureAwait(false);
      return tokens;
    }).ConfigureAwait(false);
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Sign-out must settle even if an older token operation failed.")]
  public async Task SignOutAsync(CancellationToken cancellationToken = default)
  {
    await BeginLocalSignOutAsync().ConfigureAwait(false);
    try
    {
      var tokens = await store.LoadAsync(scope).ConfigureAwait(false);
      if (tokens is not null)
        await tokenClient.RevokeAsync(tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      await store.ClearAsync(scope).ConfigureAwait(false);
      lock (gate) { latestRefreshedTokens = null; }
    }
  }

  /// <summary>Fences future token writes and drains local writes without waiting for a stalled exchange.</summary>
  internal async Task BeginLocalSignOutAsync()
  {
    lock (gate) { signedOut = true; }
    _ = exchangeCancellation.CancelAsync();
    await localMutation.WaitAsync().ConfigureAwait(false);
    try { lock (gate) { latestRefreshedTokens = null; } }
    finally { localMutation.Release(); }
  }

  public Task ClearAsync(string? rejectedAccessToken = null)
  {
    lock (gate)
    {
      if (signedOut) throw new McpUnauthorizedException();
      if (clearTask is not null) return clearTask;
      clearing = true;
      var pending = ClearCoreAsync(refreshTask, redeemTask, rejectedAccessToken);
      clearTask = pending;
      _ = pending.ContinueWith(completed =>
      {
        _ = completed.Exception;
        lock (gate)
        {
          if (!ReferenceEquals(clearTask, completed)) return;
          clearTask = null;
          clearing = false;
        }
      }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
      return pending;
    }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Invalid-token cleanup must settle even if an older token operation failed.")]
  private async Task ClearCoreAsync(
      Task<MemberMcpOAuthTokens>? refreshing, Task<MemberMcpOAuthTokens>? redeeming,
      string? rejectedAccessToken)
  {
    if (refreshing is not null) { try { await refreshing.ConfigureAwait(false); } catch (Exception) { } }
    if (redeeming is not null) { try { await redeeming.ConfigureAwait(false); } catch (Exception) { } }
    await SerializeMutationAsync(async () =>
    {
      return await ClearActiveTokensAsync(rejectedAccessToken).ConfigureAwait(false);
    }).ConfigureAwait(false);
  }

  private async Task SaveActiveTokensAsync(MemberMcpOAuthTokens tokens)
  {
    await localMutation.WaitAsync().ConfigureAwait(false);
    try
    {
      lock (gate) { if (signedOut) throw new McpUnauthorizedException(); }
      await store.SaveAsync(scope, tokens).ConfigureAwait(false);
      lock (gate) { latestRefreshedTokens = tokens; }
    }
    finally { localMutation.Release(); }
  }

  private async Task<bool> ClearActiveTokensAsync(string? rejectedAccessToken)
  {
    await localMutation.WaitAsync().ConfigureAwait(false);
    try
    {
      lock (gate) { if (signedOut) return false; }
      var current = await store.LoadAsync(scope).ConfigureAwait(false);
      if (rejectedAccessToken is not null && current?.AccessToken != rejectedAccessToken) return false;
      await store.ClearAsync(scope).ConfigureAwait(false);
      lock (gate) { latestRefreshedTokens = null; }
      return true;
    }
    finally { localMutation.Release(); }
  }

  private void ObserveCompletion(Task<MemberMcpOAuthTokens> task, bool refresh) =>
      _ = task.ContinueWith(completed =>
      {
        _ = completed.Exception;
        lock (gate)
        {
          if (refresh && ReferenceEquals(refreshTask, completed)) refreshTask = null;
          if (!refresh && ReferenceEquals(redeemTask, completed)) redeemTask = null;
        }
      }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}

namespace Voucha.Client.Core.Agent;

/// <summary>Refresh-token rotation is single-flight for one signed-in member.</summary>
public sealed partial class MemberMcpTokenManager(
    string accountId,
    IMemberMcpOAuthTokenStore store,
    MemberMcpOAuthTokenClient tokenClient)
{
  private readonly object gate = new();
  private Task mutationTail = Task.CompletedTask;
  private readonly MemberMcpOAuthTokenScope scope = tokenClient.ScopeFor(accountId);
  private Task<MemberMcpOAuthTokens>? refreshTask;
  private Task<MemberMcpOAuthTokens>? redeemTask;
  private MemberMcpOAuthTokens? latestRefreshedTokens;
  private bool signedOut;
  private bool clearing;

  public async Task<string> AccessTokenAsync()
  {
    lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
    var tokens = await store.LoadAsync(scope).ConfigureAwait(false)
        ?? throw new McpUnauthorizedException();
    lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
    if (tokens.AcquiredAt.AddSeconds(tokens.ExpiresIn - 30) <= DateTimeOffset.UtcNow)
      return await RefreshAsync().ConfigureAwait(false);
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
      var tokens = await tokenClient.RedeemAsync(code, verifier, redirectUri).ConfigureAwait(false);
      await store.SaveAsync(scope, tokens).ConfigureAwait(false);
      lock (gate) { latestRefreshedTokens = tokens; }
      return tokens;
    }).ConfigureAwait(false);
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Sign-out must settle even if an older token operation failed.")]
  public async Task SignOutAsync(CancellationToken cancellationToken = default)
  {
    Task<MemberMcpOAuthTokens>? refreshing;
    Task<MemberMcpOAuthTokens>? redeeming;
    lock (gate)
    {
      signedOut = true;
      refreshing = refreshTask;
      redeeming = redeemTask;
    }
    if (refreshing is not null) { try { await refreshing.ConfigureAwait(false); } catch (Exception) { } }
    if (redeeming is not null) { try { await redeeming.ConfigureAwait(false); } catch (Exception) { } }
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

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Invalid-token cleanup must settle even if an older token operation failed.")]
  public async Task ClearAsync()
  {
    Task<MemberMcpOAuthTokens>? refreshing;
    Task<MemberMcpOAuthTokens>? redeeming;
    lock (gate)
    {
      if (signedOut) throw new McpUnauthorizedException();
      clearing = true;
      refreshing = refreshTask;
      redeeming = redeemTask;
    }
    if (refreshing is not null) { try { await refreshing.ConfigureAwait(false); } catch (Exception) { } }
    if (redeeming is not null) { try { await redeeming.ConfigureAwait(false); } catch (Exception) { } }
    try
    {
      await store.ClearAsync(scope).ConfigureAwait(false);
      lock (gate) { latestRefreshedTokens = null; }
    }
    finally { lock (gate) { clearing = false; } }
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

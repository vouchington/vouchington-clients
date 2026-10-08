namespace Voucha.Client.Core.Agent;

/// <summary>Refresh-token rotation is single-flight for one signed-in member.</summary>
public sealed class MemberMcpTokenManager(
    string accountId,
    IMemberMcpOAuthTokenStore store,
    MemberMcpOAuthTokenClient tokenClient)
{
  private readonly object gate = new();
  private Task<MemberMcpOAuthTokens>? refreshTask;
  private Task<MemberMcpOAuthTokens>? redeemTask;
  private MemberMcpOAuthTokens? latestRefreshedTokens;
  private bool signedOut;

  public async Task<string> AccessTokenAsync()
  {
    lock (gate) { if (signedOut) throw new McpUnauthorizedException(); }
    var tokens = await store.LoadAsync(accountId).ConfigureAwait(false)
        ?? throw new McpUnauthorizedException();
    lock (gate) { if (signedOut) throw new McpUnauthorizedException(); }
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
      if (signedOut || redeemTask is not null) throw new McpUnauthorizedException();
      redeemTask = RedeemCoreAsync(code, verifier, redirectUri);
      pending = redeemTask;
    }
    MemberMcpOAuthTokens tokens;
    try { tokens = await pending.WaitAsync(cancellationToken).ConfigureAwait(false); }
    finally
    {
      if (pending.IsCompleted)
      {
        lock (gate) { if (ReferenceEquals(redeemTask, pending)) redeemTask = null; }
      }
    }
    lock (gate) { if (signedOut) throw new McpUnauthorizedException(); }
    return tokens.AccessToken;
  }

  private async Task<MemberMcpOAuthTokens> RedeemCoreAsync(string code, string verifier, Uri redirectUri)
  {
    var tokens = await tokenClient.RedeemAsync(code, verifier, redirectUri).ConfigureAwait(false);
    await store.SaveAsync(accountId, tokens).ConfigureAwait(false);
    return tokens;
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
    var tokens = await store.LoadAsync(accountId).ConfigureAwait(false);
    try
    {
      if (tokens is not null)
        await tokenClient.RevokeAsync(tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      await store.ClearAsync(accountId).ConfigureAwait(false);
    }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Invalid-token cleanup must settle even if an older token operation failed.")]
  public async Task ClearAsync()
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
    await store.ClearAsync(accountId).ConfigureAwait(false);
  }

  public async Task<string> RefreshAsync(CancellationToken cancellationToken = default)
  {
    Task<MemberMcpOAuthTokens> pending;
    lock (gate)
    {
      if (signedOut) throw new McpUnauthorizedException();
      refreshTask ??= RefreshCoreAsync();
      pending = refreshTask;
    }
    try
    {
      var result = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
      lock (gate) { if (signedOut) throw new McpUnauthorizedException(); }
      return result.AccessToken;
    }
    finally
    {
      if (pending.IsCompleted)
      {
        lock (gate)
        {
          if (ReferenceEquals(refreshTask, pending)) refreshTask = null;
        }
      }
    }
  }

  private async Task<MemberMcpOAuthTokens> RefreshCoreAsync()
  {
    var previous = await store.LoadAsync(accountId).ConfigureAwait(false)
        ?? throw new McpUnauthorizedException();
    MemberMcpOAuthTokens next;
    try
    {
      next = await tokenClient.RefreshAsync(previous.RefreshToken).ConfigureAwait(false);
    }
    catch (McpUnauthorizedException)
    {
      await store.ClearAsync(accountId).ConfigureAwait(false);
      throw;
    }
    try
    {
      await store.SaveAsync(accountId, next).ConfigureAwait(false);
    }
    catch
    {
      await store.ClearAsync(accountId).ConfigureAwait(false);
      throw;
    }
    lock (gate) { latestRefreshedTokens = next; }
    return next;
  }

  /// <summary>Reuse a completed rotation when an older in-flight MCP call reports 401 late.</summary>
  public async Task<string> RefreshIfCurrentAsync(
      string rejectedAccessToken, CancellationToken cancellationToken = default)
  {
    Task<MemberMcpOAuthTokens>? pending;
    MemberMcpOAuthTokens? latest;
    lock (gate)
    {
      if (signedOut) throw new McpUnauthorizedException();
      pending = refreshTask;
      latest = latestRefreshedTokens;
    }
    if (pending is not null)
    {
      var rotated = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
      lock (gate) { if (signedOut) throw new McpUnauthorizedException(); }
      return rotated.AccessToken;
    }
    if (latest is not null && latest.AccessToken != rejectedAccessToken &&
        latest.AcquiredAt.AddSeconds(latest.ExpiresIn - 30) > DateTimeOffset.UtcNow)
      return latest.AccessToken;

    var current = await store.LoadAsync(accountId).ConfigureAwait(false)
        ?? throw new McpUnauthorizedException();
    lock (gate)
    {
      if (signedOut) throw new McpUnauthorizedException();
      pending = refreshTask;
      latest = latestRefreshedTokens;
    }
    if (pending is not null)
    {
      var rotated = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
      lock (gate) { if (signedOut) throw new McpUnauthorizedException(); }
      return rotated.AccessToken;
    }
    if (latest is not null && latest.AccessToken != rejectedAccessToken &&
        latest.AcquiredAt.AddSeconds(latest.ExpiresIn - 30) > DateTimeOffset.UtcNow)
      return latest.AccessToken;
    if (current.AccessToken != rejectedAccessToken &&
        current.AcquiredAt.AddSeconds(current.ExpiresIn - 30) > DateTimeOffset.UtcNow)
      return current.AccessToken;
    return await RefreshAsync(cancellationToken).ConfigureAwait(false);
  }
}

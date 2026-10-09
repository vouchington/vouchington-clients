namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpTokenManager
{
  public async Task<string> RefreshAsync(CancellationToken cancellationToken = default)
  {
    Task<MemberMcpOAuthTokens> pending;
    lock (gate)
    {
      if (signedOut || clearing || redeemTask is not null) throw new McpUnauthorizedException();
      if (refreshTask is null)
      {
        var started = RefreshCoreAsync();
        refreshTask = started;
        pending = started;
        ObserveCompletion(started, refresh: true);
      }
      else pending = refreshTask;
    }
    var result = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
    lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
    return result.AccessToken;
  }

  private async Task<MemberMcpOAuthTokens> RefreshCoreAsync()
  {
    return await SerializeMutationAsync(async () =>
    {
      var previous = await store.LoadAsync(scope).ConfigureAwait(false)
          ?? throw new McpUnauthorizedException();
      MemberMcpOAuthTokens next;
      try { next = await tokenClient.RefreshAsync(previous.RefreshToken).ConfigureAwait(false); }
      catch (McpUnauthorizedException)
      {
        await store.ClearAsync(scope).ConfigureAwait(false);
        lock (gate) { latestRefreshedTokens = null; }
        throw;
      }
      try { await store.SaveAsync(scope, next).ConfigureAwait(false); }
      catch
      {
        await store.ClearAsync(scope).ConfigureAwait(false);
        lock (gate) { latestRefreshedTokens = null; }
        throw;
      }
      lock (gate) { latestRefreshedTokens = next; }
      return next;
    }).ConfigureAwait(false);
  }

  /// <summary>Reuse a completed rotation when an older in-flight MCP call reports 401 late.</summary>
  public async Task<string> RefreshIfCurrentAsync(
      string rejectedAccessToken, CancellationToken cancellationToken = default)
  {
    Task<MemberMcpOAuthTokens>? pending;
    Task<MemberMcpOAuthTokens>? redeeming;
    MemberMcpOAuthTokens? latest;
    lock (gate)
    {
      if (signedOut || clearing) throw new McpUnauthorizedException();
      pending = refreshTask;
      redeeming = redeemTask;
      latest = latestRefreshedTokens;
    }
    if (redeeming is not null)
    {
      var redeemed = await redeeming.WaitAsync(cancellationToken).ConfigureAwait(false);
      lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
      return redeemed.AccessToken;
    }
    if (pending is not null)
    {
      var rotated = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
      lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
      return rotated.AccessToken;
    }
    if (latest is not null && latest.AccessToken != rejectedAccessToken &&
        latest.AcquiredAt.AddSeconds(latest.ExpiresIn - 30) > DateTimeOffset.UtcNow)
      return latest.AccessToken;

    var current = await store.LoadAsync(scope).ConfigureAwait(false)
        ?? throw new McpUnauthorizedException();
    lock (gate)
    {
      if (signedOut || clearing) throw new McpUnauthorizedException();
      pending = refreshTask;
      redeeming = redeemTask;
      latest = latestRefreshedTokens;
    }
    if (redeeming is not null)
    {
      var redeemed = await redeeming.WaitAsync(cancellationToken).ConfigureAwait(false);
      lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
      return redeemed.AccessToken;
    }
    if (pending is not null)
    {
      var rotated = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
      lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
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

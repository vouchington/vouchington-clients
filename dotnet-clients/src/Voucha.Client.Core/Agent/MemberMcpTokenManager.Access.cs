using System.Diagnostics;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpTokenManager
{
  private static readonly TimeSpan RedemptionWaitTimeout = TimeSpan.FromSeconds(10);

  public async Task<string> AccessTokenAsync(CancellationToken cancellationToken = default)
  {
    var started = Stopwatch.GetTimestamp();
    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();
      Task<MemberMcpOAuthTokens>? redeeming;
      long revision;
      lock (gate)
      {
        if (signedOut || clearing) throw new McpUnauthorizedException();
        redeeming = redeemTask;
        revision = redemptionRevision;
      }
      if (redeeming is not null)
        await WaitForRedemptionAsync(redeeming, started, cancellationToken).ConfigureAwait(false);
      lock (gate) { if (signedOut || clearing) throw new McpUnauthorizedException(); }
      var tokens = await store.LoadAsync(scope).WaitAsync(cancellationToken).ConfigureAwait(false);
      cancellationToken.ThrowIfCancellationRequested();
      lock (gate)
      {
        if (signedOut || clearing) throw new McpUnauthorizedException();
        if (redemptionRevision != revision || redeemTask is { IsCompleted: false }) continue;
      }
      if (tokens is null) throw new McpUnauthorizedException();
      if (tokens.AcquiredAt.AddSeconds(tokens.ExpiresIn - 30) <= DateTimeOffset.UtcNow)
        return await RefreshAsync(cancellationToken).ConfigureAwait(false);
      return tokens.AccessToken;
    }
  }

  private static async Task WaitForRedemptionAsync(
      Task<MemberMcpOAuthTokens> pending, long started, CancellationToken cancellationToken)
  {
    try
    {
      if (pending.IsCompleted) await pending.ConfigureAwait(false);
      else
      {
        var remaining = RedemptionWaitTimeout - Stopwatch.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero) throw new TimeoutException("MCP code redemption has not settled.");
        await pending.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
      }
    }
    catch (TimeoutException) when (!pending.IsCompleted) { throw; }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
    // A rejected new code leaves the previous stored grant usable; read its current value below.
    catch (Exception error) when (error is not OutOfMemoryException and not InvalidDataException) { }
  }
}

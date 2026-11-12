using System.Net;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Agent;

/// <summary>
/// Holds the session-bound MCP access token for one signed-in account in memory only. The token is
/// minted through the cookie-bearing API client, never persisted, and never written to refresh-token
/// storage. Mint is single-flight; <see cref="Clear"/> fences any mint still in flight.
/// </summary>
public sealed class MemberMcpSessionTokenSource : IDisposable
{
  private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(30);
  private readonly VouchaApiClient api;
  private readonly Uri resource;
  private readonly string clientApp;
  private readonly TimeProvider time;
  private readonly Func<bool> stillCurrent;
  private readonly CancellationTokenSource lifetime = new();
  private readonly object gate = new();
  private Minted? current;
  private Task<Minted>? minting;
  private long epoch;
  private bool cleared;

  public MemberMcpSessionTokenSource(
      VouchaApiClient api, Uri resource, string clientApp,
      Func<bool>? stillCurrent = null, TimeProvider? time = null)
  {
    ArgumentNullException.ThrowIfNull(api);
    ArgumentNullException.ThrowIfNull(resource);
    ArgumentException.ThrowIfNullOrWhiteSpace(clientApp);
    this.api = api;
    this.resource = resource;
    this.clientApp = clientApp;
    this.time = time ?? TimeProvider.System;
    this.stillCurrent = stillCurrent ?? (() => true);
  }

  /// <summary>The live token, if any, without minting; used for best-effort remote close.</summary>
  public string? CurrentAccessToken()
  {
    lock (gate) return cleared ? null : current?.AccessToken;
  }

  public async Task<string> AccessTokenAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    Task<Minted> pending;
    lock (gate)
    {
      if (cleared) throw new McpUnauthorizedException();
      if (!stillCurrent()) { ClearLocked(); throw new McpUnauthorizedException(); }
      if (current is { } live && live.ExpiresAt > time.GetUtcNow()) return live.AccessToken;
      current = null;
      var started = epoch;
      pending = minting ??= Task.Run(() => MintAsync(started), CancellationToken.None);
    }
    var minted = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
    lock (gate)
    {
      if (cleared) throw new McpUnauthorizedException();
      // The cookie jar may have moved to another account while the mint was in flight.
      if (!stillCurrent()) { ClearLocked(); throw new McpUnauthorizedException(); }
    }
    return minted.AccessToken;
  }

  /// <summary>A late 401 for an older token reuses a newer mint instead of minting again.</summary>
  public Task<string> RefreshIfCurrentAsync(
      string rejectedAccessToken, CancellationToken cancellationToken = default)
  {
    lock (gate)
    {
      if (current is { } live && live.AccessToken == rejectedAccessToken) current = null;
    }
    return AccessTokenAsync(cancellationToken);
  }

  /// <summary>Drops the token at sign-out or account change and fences any mint in flight.</summary>
  public void Clear()
  {
    lock (gate) ClearLocked();
  }

  private void ClearLocked()
  {
    cleared = true;
    current = null;
    minting = null;
    epoch++;
    _ = lifetime.CancelAsync();
  }

  private async Task<Minted> MintAsync(long mintEpoch)
  {
    try
    {
      NativeSessionMcpTokenResponse response;
      try
      {
        response = await api
            .SendAsync<NativeSessionMcpTokenResponse>(
                VouchaApiEndpoints.MintNativeSessionMcpToken(clientApp), lifetime.Token)
            .ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
      {
        throw new McpUnauthorizedException();
      }
      catch (VouchaApiException error) when (error.StatusCode == HttpStatusCode.Unauthorized)
      {
        throw new McpUnauthorizedException("The native session cannot mint an MCP token.", error);
      }
      var minted = Validate(response);
      lock (gate)
      {
        if (cleared || epoch != mintEpoch) throw new McpUnauthorizedException();
        current = minted;
      }
      return minted;
    }
    finally
    {
      lock (gate) { if (epoch == mintEpoch) minting = null; }
    }
  }

  private Minted Validate(NativeSessionMcpTokenResponse response)
  {
    if (!string.Equals(response.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(response.AccessToken) || response.ExpiresIn <= 0 ||
        !Uri.TryCreate(response.Resource, UriKind.Absolute, out var granted) || granted != resource)
      throw new InvalidDataException("The minted MCP token does not match the MCP resource.");
    return new Minted(
        response.AccessToken, time.GetUtcNow().AddSeconds(response.ExpiresIn) - ExpirySkew);
  }

  public void Dispose()
  {
    Clear();
    lifetime.Dispose();
  }

  private sealed record Minted(string AccessToken, DateTimeOffset ExpiresAt);
}

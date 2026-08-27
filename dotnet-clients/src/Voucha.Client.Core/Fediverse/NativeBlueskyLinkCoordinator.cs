using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Fediverse;

public sealed class NativeBlueskyLinkCoordinator(
    VouchaApiClient client,
    INativeBlueskyLinkPersistence persistence,
    INativeExternalBrowser browser,
    ISessionStore sessionStore) : ObservableObject
{
  private NativeBlueskyLinkState state;
  private string? errorCode;

  public NativeBlueskyLinkState State { get => state; private set => SetProperty(ref state, value); }
  public string? ErrorCode { get => errorCode; private set => SetProperty(ref errorCode, value); }
  public bool IsLinked => sessionStore.Current.Identity?.BlueskyAccount is not null;
  public string? LinkedHandle => sessionStore.Current.Identity?.BlueskyAccount?.Handle;

  public async Task StartAsync(
      string handle,
      DateTimeOffset? now = null,
      CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(handle)) throw new ArgumentException("A Bluesky handle is required.", nameof(handle));
    State = NativeBlueskyLinkState.Connecting;
    ErrorCode = null;
    var proof = NativeBlueskyCompletionProof.Create();
    try
    {
      var response = await client.BeginNativeBlueskyAccountLinkAsync(
          handle.Trim(), proof.Challenge, cancellationToken).ConfigureAwait(true);
      if (response.FlowId is not { Length: > 0 } flowId) throw new InvalidOperationException("Native link flow was not returned.");
      await persistence.WriteAsync(new(flowId, proof.Verifier, now ?? DateTimeOffset.UtcNow), cancellationToken)
          .ConfigureAwait(true);
      if (!await browser.OpenAsync(response.RedirectUrl, cancellationToken).ConfigureAwait(true))
      {
        await persistence.ClearAsync(cancellationToken).ConfigureAwait(true);
        throw new InvalidOperationException("The system browser could not open.");
      }
      State = NativeBlueskyLinkState.WaitingForCallback;
    }
    catch
    {
      State = NativeBlueskyLinkState.Failed;
      throw;
    }
  }

  public async Task<bool> HandleCallbackAsync(Uri uri, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(uri);
    if (!IsCallback(uri)) return false;
    var query = ParseQuery(uri.Query);
    if (!query.TryGetValue("flow_id", out var flowId) || string.IsNullOrWhiteSpace(flowId)) return false;
    if (query.TryGetValue("bluesky_error", out var providerError))
    {
      if (!await persistence.ClaimFailureAsync(flowId, cancellationToken).ConfigureAwait(true)) return false;
      ErrorCode = providerError;
      State = NativeBlueskyLinkState.Failed;
      return true;
    }
    if (!query.TryGetValue("completion_token", out var token) || string.IsNullOrWhiteSpace(token)) return false;
    if (!await persistence.ClaimCallbackAsync(flowId, token, cancellationToken).ConfigureAwait(true)) return false;
    var pending = await persistence.ReadAsync(cancellationToken).ConfigureAwait(true);
    if (pending is null || DateTimeOffset.UtcNow >= pending.ExpiresAt)
    {
      await persistence.ClearAsync(cancellationToken).ConfigureAwait(true);
      State = NativeBlueskyLinkState.Expired;
      return true;
    }
    await FinalizeAsync(pending, cancellationToken).ConfigureAwait(true);
    return true;
  }

  public async Task ResumeAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
  {
    var pending = await persistence.ReadAsync(cancellationToken).ConfigureAwait(true);
    if (pending is null) return;
    if (now >= pending.ExpiresAt)
    {
      await persistence.ClearAsync(cancellationToken).ConfigureAwait(true);
      State = NativeBlueskyLinkState.Expired;
      return;
    }
    if (pending.CompletionToken is not null)
    {
      await FinalizeAsync(pending, cancellationToken).ConfigureAwait(true);
      return;
    }
    State = NativeBlueskyLinkState.WaitingForCallback;
  }

  public async Task CancelAsync(CancellationToken cancellationToken = default)
  {
    await persistence.ClearAsync(cancellationToken).ConfigureAwait(true);
    State = NativeBlueskyLinkState.Cancelled;
  }

  public async Task DisconnectAsync(CancellationToken cancellationToken = default)
  {
    State = NativeBlueskyLinkState.Disconnecting;
    try
    {
      await client.DisconnectBlueskyAccountAsync(cancellationToken).ConfigureAwait(true);
      await RefreshIdentityAsync(cancellationToken).ConfigureAwait(true);
      OnPropertyChanged(nameof(IsLinked));
      OnPropertyChanged(nameof(LinkedHandle));
      State = NativeBlueskyLinkState.Idle;
    }
    catch (Exception ex) when (
        !cancellationToken.IsCancellationRequested &&
        ex is VouchaApiException or HttpRequestException)
    {
      State = NativeBlueskyLinkState.Failed;
      throw;
    }
  }

  private static bool IsCallback(Uri uri) =>
      uri.Scheme.Equals("voucha", StringComparison.OrdinalIgnoreCase) && uri.Host == "auth" && uri.AbsolutePath == "/bluesky/callback";

  private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?')
      .Split('&', StringSplitOptions.RemoveEmptyEntries)
      .Select(item => item.Split('=', 2))
      .Where(parts => parts.Length == 2)
      .GroupBy(parts => Uri.UnescapeDataString(parts[0]), StringComparer.Ordinal)
      .ToDictionary(group => group.Key, group => Uri.UnescapeDataString(group.First()[1]), StringComparer.Ordinal);

  private async Task FinalizeAsync(PendingNativeBlueskyLink pending, CancellationToken cancellationToken)
  {
    State = NativeBlueskyLinkState.Finalizing;
    try
    {
      await client.CompleteNativeBlueskyAccountLinkAsync(
          pending.FlowId,
          pending.CompletionToken ?? throw new InvalidOperationException("Completion token is missing."),
          pending.CompletionProofVerifier,
          cancellationToken).ConfigureAwait(true);
      await ReconcileFinalizedLinkAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex) when (
        !cancellationToken.IsCancellationRequested &&
        ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      try
      {
        await ReconcileFinalizedLinkAsync(cancellationToken).ConfigureAwait(true);
      }
      catch (Exception refreshError) when (
          !cancellationToken.IsCancellationRequested &&
          refreshError is VouchaApiException or HttpRequestException or InvalidOperationException)
      {
        State = NativeBlueskyLinkState.Failed;
        return;
      }
    }
  }

  private async Task ReconcileFinalizedLinkAsync(CancellationToken cancellationToken)
  {
    await RefreshIdentityAsync(cancellationToken).ConfigureAwait(true);
    OnPropertyChanged(nameof(IsLinked));
    OnPropertyChanged(nameof(LinkedHandle));
    if (!IsLinked)
    {
      State = NativeBlueskyLinkState.Failed;
      return;
    }
    await persistence.ClearAsync(cancellationToken).ConfigureAwait(true);
    State = NativeBlueskyLinkState.Connected;
  }

  private Task RefreshIdentityAsync(CancellationToken cancellationToken) =>
      sessionStore is IForcedSessionRefreshStore forced
          ? forced.RefreshAsync(force: true, cancellationToken)
          : sessionStore.RefreshAsync(cancellationToken);
}

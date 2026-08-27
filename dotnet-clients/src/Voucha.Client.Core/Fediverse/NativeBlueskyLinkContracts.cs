namespace Voucha.Client.Core.Fediverse;

public sealed record PendingNativeBlueskyLink(
    string FlowId,
    string CompletionProofVerifier,
    DateTimeOffset StartedAt,
    string? CompletionToken = null)
{
  public DateTimeOffset ExpiresAt => StartedAt.AddMinutes(10);
}

public interface INativeBlueskyLinkPersistence
{
  Task<PendingNativeBlueskyLink?> ReadAsync(CancellationToken cancellationToken = default);
  Task WriteAsync(PendingNativeBlueskyLink pending, CancellationToken cancellationToken = default);
  Task<bool> ClaimCallbackAsync(
      string flowId,
      string completionToken,
      CancellationToken cancellationToken = default);
  Task<bool> ClaimFailureAsync(string flowId, CancellationToken cancellationToken = default);
  Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface INativeExternalBrowser
{
  Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}

public enum NativeBlueskyLinkState
{
  Idle,
  Connecting,
  WaitingForCallback,
  Finalizing,
  Connected,
  Cancelled,
  Expired,
  Failed,
  Disconnecting,
}

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed record PendingNativeOAuthAuthorization(
    string FlowId,
    OAuthBrokerProvider Provider,
    OAuthAuthorizationPurpose Purpose,
    string CompletionProofVerifier,
    DateTimeOffset ExpiresAt,
    string? CompletionToken = null);

public enum NativeOAuthAuthorizationResultKind
{
  Authenticated,
  MfaRequired,
  Connected,
  Expired,
}

public sealed record NativeOAuthAuthorizationResult(
    NativeOAuthAuthorizationResultKind Kind,
    OAuthBrokerProvider Provider,
    OAuthAuthorizationPurpose Purpose,
    string? LoginAttemptId = null);

public readonly record struct NativeOAuthCallbackOutcome(
    bool Handled,
    OAuthAuthorizationPurpose? Purpose);

public readonly record struct NativeOAuthAuthorizationSnapshot(
    PendingNativeOAuthAuthorization? Pending,
    NativeOAuthAuthorizationResult? Result);

public interface INativeOAuthAuthorizationPersistence
{
  Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
      CancellationToken cancellationToken = default);

  Task WritePendingAsync(
      PendingNativeOAuthAuthorization pending,
      CancellationToken cancellationToken = default);

  Task<bool> ClaimCallbackAsync(
      string flowId,
      string completionToken,
      CancellationToken cancellationToken = default);

  Task<bool> CompleteAsync(
      string flowId,
      NativeOAuthAuthorizationResult result,
      CancellationToken cancellationToken = default);

  Task ClearPendingAsync(CancellationToken cancellationToken = default);

  Task AcknowledgeResultAsync(CancellationToken cancellationToken = default);
}

public interface ISupersededNativeOAuthAuthorizationDiscarder
{
  Task DiscardAfterSuccessfulAuthenticationAsync();
}

public enum NativeOAuthAuthorizationState
{
  Idle,
  Connecting,
  WaitingForCallback,
  Finalizing,
  Authenticated,
  MfaRequired,
  Connected,
  Cancelled,
  Expired,
  Failed,
  Disconnecting,
}

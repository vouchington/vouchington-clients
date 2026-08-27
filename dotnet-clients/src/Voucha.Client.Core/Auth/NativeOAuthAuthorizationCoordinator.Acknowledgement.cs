using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinator
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Authentication has already succeeded, so secure-state cleanup failures must remain observable without changing that outcome.")]
  public async Task DiscardAfterSuccessfulAuthenticationAsync()
  {
    InvalidateStartsAfterSuccessfulAuthentication();
    ErrorMessage = null;
    try
    {
      await persistence.ClearPendingAsync(CancellationToken.None).ConfigureAwait(true);
      await persistence.AcknowledgeResultAsync(CancellationToken.None).ConfigureAwait(true);
      Pending = null;
      Result = null;
      State = NativeOAuthAuthorizationState.Idle;
    }
    catch (Exception ex)
    {
      RecordFailure(ex.Message);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Secure-storage failures must remain retryable instead of escaping MAUI event handlers.")]
  public async Task AcknowledgeResultAsync(CancellationToken cancellationToken = default)
  {
    if (Result?.Kind == NativeOAuthAuthorizationResultKind.MfaRequired &&
        !sessionStore.Current.IsAuthenticated)
    {
      return;
    }
    ErrorMessage = null;
    try
    {
      await persistence.AcknowledgeResultAsync(cancellationToken).ConfigureAwait(true);
      Result = null;
      if (Pending is null) State = NativeOAuthAuthorizationState.Idle;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      RecordFailure(ex.Message);
    }
  }
}

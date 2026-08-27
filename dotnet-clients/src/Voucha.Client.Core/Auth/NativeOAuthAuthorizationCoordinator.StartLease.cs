using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinator
{
  private readonly object startLeaseSync = new();
  private bool startLeaseHeld;
  private bool disposed;
  private long successfulAuthenticationDiscardGeneration;

  private long CaptureSuccessfulAuthenticationDiscardGeneration()
  {
    lock (startLeaseSync) return successfulAuthenticationDiscardGeneration;
  }

  private void InvalidateStartsAfterSuccessfulAuthentication()
  {
    lock (startLeaseSync) successfulAuthenticationDiscardGeneration++;
  }

  private bool WasStartInvalidatedBySuccessfulAuthentication(
      long generation,
      OAuthAuthorizationPurpose purpose) =>
      Interlocked.Read(ref successfulAuthenticationDiscardGeneration) != generation ||
      purpose == OAuthAuthorizationPurpose.Authenticate && sessionStore.Current.IsAuthenticated;

  private Task<bool>? TryOpenBrowserForCurrentStart(
      long generation,
      OAuthAuthorizationPurpose purpose,
      Uri redirectUrl,
      CancellationToken cancellationToken)
  {
    lock (startLeaseSync)
    {
      if (WasStartInvalidatedBySuccessfulAuthentication(generation, purpose)) return null;
      return browser.OpenAsync(redirectUrl, cancellationToken);
    }
  }

  private async Task<bool> StopInvalidatedStartAsync(
      long discardGeneration,
      OAuthAuthorizationPurpose purpose,
      bool pendingMayHaveBeenPersisted)
  {
    if (!WasStartInvalidatedBySuccessfulAuthentication(discardGeneration, purpose)) return false;
    var cleanupError = await ClearFailedStartAsync(pendingMayHaveBeenPersisted)
        .ConfigureAwait(true);
    if (cleanupError is null)
    {
      if (State != NativeOAuthAuthorizationState.Failed)
      {
        ErrorMessage = null;
        State = NativeOAuthAuthorizationState.Idle;
      }
    }
    else
    {
      RecordFailure(cleanupError.Message);
    }
    return true;
  }

  private bool TryAcquireStartLease()
  {
    lock (startLeaseSync)
    {
      ObjectDisposedException.ThrowIf(disposed, this);
      if (startLeaseHeld) return false;
      startLeaseHeld = true;
      return true;
    }
  }

  private void ReleaseStartLease()
  {
    lock (startLeaseSync)
    {
      startLeaseHeld = false;
    }
  }

  private void DisposeStartLease()
  {
    lock (startLeaseSync)
    {
      disposed = true;
    }
  }
}

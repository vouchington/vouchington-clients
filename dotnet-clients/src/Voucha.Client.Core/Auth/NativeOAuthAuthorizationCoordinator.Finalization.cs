using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinator
{
  private async Task FinalizeAsync(
      PendingNativeOAuthAuthorization authorization,
      CancellationToken cancellationToken)
  {
    if (!await finalizationGate.WaitAsync(0, cancellationToken).ConfigureAwait(true)) return;
    State = NativeOAuthAuthorizationState.Finalizing;
    ErrorMessage = null;
    try
    {
      while (now() < authorization.ExpiresAt)
      {
        var completion = await client.CompleteOAuthAuthorizationAsync(
            authorization.FlowId,
            authorization.CompletionToken ??
                throw new InvalidOperationException("OAuth completion token is missing."),
            authorization.CompletionProofVerifier,
            cancellationToken).ConfigureAwait(true);
        if (completion.Kind == OAuthCompletionKind.Pending)
        {
          await delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(true);
          continue;
        }

        var result = await BuildResultAsync(
            authorization,
            completion,
            cancellationToken).ConfigureAwait(true);
        if (await persistence.CompleteAsync(
            authorization.FlowId,
            result,
            cancellationToken).ConfigureAwait(true))
        {
          Pending = null;
          Result = result;
          State = StateFor(result.Kind);
        }
        return;
      }
      await CompleteExpiredAsync(authorization, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      State = NativeOAuthAuthorizationState.WaitingForCallback;
      throw;
    }
    catch (Exception ex) when (
        ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      RecordFailure(ex.Message);
    }
    finally
    {
      finalizationGate.Release();
    }
  }

  private async Task<NativeOAuthAuthorizationResult> BuildResultAsync(
      PendingNativeOAuthAuthorization authorization,
      OAuthCompletionResponse completion,
      CancellationToken cancellationToken)
  {
    switch (completion.Kind)
    {
      case OAuthCompletionKind.Authenticated:
        await RefreshSessionAsync(cancellationToken).ConfigureAwait(true);
        if (!sessionStore.Current.IsAuthenticated)
        {
          throw new InvalidOperationException("OAuth authentication could not be confirmed.");
        }
        return new(
            NativeOAuthAuthorizationResultKind.Authenticated,
            authorization.Provider,
            authorization.Purpose);
      case OAuthCompletionKind.MfaRequired:
        return new(
            NativeOAuthAuthorizationResultKind.MfaRequired,
            authorization.Provider,
            authorization.Purpose,
            completion.LoginAttemptId ??
                throw new InvalidOperationException("OAuth MFA attempt is missing."));
      case OAuthCompletionKind.Connected:
        await RefreshSessionAsync(cancellationToken).ConfigureAwait(true);
        if (!IsProviderInSession(authorization.Provider))
        {
          throw new InvalidOperationException("OAuth account connection could not be confirmed.");
        }
        ClearCommittedDisconnect(authorization.Provider);
        return new(
            NativeOAuthAuthorizationResultKind.Connected,
            authorization.Provider,
            authorization.Purpose);
      default:
        throw new InvalidOperationException("OAuth completion outcome is invalid.");
    }
  }

  private async Task CompleteExpiredAsync(
      PendingNativeOAuthAuthorization authorization,
      CancellationToken cancellationToken)
  {
    var result = new NativeOAuthAuthorizationResult(
        NativeOAuthAuthorizationResultKind.Expired,
        authorization.Provider,
        authorization.Purpose);
    if (!await persistence.CompleteAsync(authorization.FlowId, result, cancellationToken)
        .ConfigureAwait(true))
    {
      await SynchronizeAsync(cancellationToken).ConfigureAwait(true);
      if (Result is null)
      {
        State = Pending is null
            ? NativeOAuthAuthorizationState.Idle
            : NativeOAuthAuthorizationState.WaitingForCallback;
      }
      return;
    }
    Pending = null;
    Result = result;
    State = NativeOAuthAuthorizationState.Expired;
  }

  private bool IsProviderInSession(OAuthBrokerProvider provider)
  {
    var identity = sessionStore.Current.Identity;
    return provider switch
    {
      OAuthBrokerProvider.Facebook => identity?.FacebookAccount is not null,
      OAuthBrokerProvider.X => identity?.XAccount is not null,
      OAuthBrokerProvider.Github => identity?.GithubAccount is not null,
      _ => false,
    };
  }

  private Task RefreshSessionAsync(CancellationToken cancellationToken) =>
      sessionStore switch
      {
        IConfirmingSessionRefreshStore confirming =>
            confirming.RefreshConfirmingAsync(cancellationToken),
        IForcedSessionRefreshStore forced => forced.RefreshAsync(force: true, cancellationToken),
        _ => sessionStore.RefreshAsync(cancellationToken),
      };
}

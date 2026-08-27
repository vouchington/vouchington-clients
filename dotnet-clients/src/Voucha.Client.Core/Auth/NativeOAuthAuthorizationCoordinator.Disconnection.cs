using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinator
{
  private readonly object committedDisconnectSync = new();
  private readonly HashSet<OAuthBrokerProvider> committedDisconnects = [];

  public bool IsProviderConnected(OAuthBrokerProvider provider)
  {
    lock (committedDisconnectSync)
    {
      if (committedDisconnects.Contains(provider)) return false;
    }
    return IsProviderInSession(provider);
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "OAuth disconnect failures are presented by MAUI and must not escape async-void handlers.")]
  public async Task DisconnectAsync(
      OAuthBrokerProvider provider,
      CancellationToken cancellationToken = default)
  {
    if (!await disconnectionGate.WaitAsync(0, cancellationToken).ConfigureAwait(true)) return;
    State = NativeOAuthAuthorizationState.Disconnecting;
    ErrorMessage = null;
    try
    {
      if (!IsDisconnectCommitted(provider))
      {
        await client.DisconnectOAuthAccountAsync(provider, cancellationToken).ConfigureAwait(true);
        MarkDisconnectCommitted(provider);
        ApplyCommittedDisconnect(provider);
      }
      await RefreshSessionAsync(cancellationToken).ConfigureAwait(true);
      if (IsProviderInSession(provider))
      {
        throw new InvalidOperationException("OAuth account disconnection could not be confirmed.");
      }
      ClearCommittedDisconnect(provider);
      State = NativeOAuthAuthorizationState.Idle;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      State = NativeOAuthAuthorizationState.Idle;
      throw;
    }
    catch (Exception ex)
    {
      RecordFailure(ex.Message);
    }
    finally
    {
      if (IsDisconnectCommitted(provider)) ApplyCommittedDisconnect(provider);
      disconnectionGate.Release();
    }
  }

  private void ApplyCommittedDisconnect(OAuthBrokerProvider provider)
  {
    if (sessionStore is ICommittedOAuthDisconnectSessionStore mutableSession)
    {
      mutableSession.ApplyCommittedOAuthDisconnect(provider);
    }
  }

  private bool IsDisconnectCommitted(OAuthBrokerProvider provider)
  {
    lock (committedDisconnectSync) return committedDisconnects.Contains(provider);
  }

  private void MarkDisconnectCommitted(OAuthBrokerProvider provider)
  {
    lock (committedDisconnectSync) committedDisconnects.Add(provider);
  }

  private void ClearCommittedDisconnect(OAuthBrokerProvider provider)
  {
    lock (committedDisconnectSync) committedDisconnects.Remove(provider);
  }
}

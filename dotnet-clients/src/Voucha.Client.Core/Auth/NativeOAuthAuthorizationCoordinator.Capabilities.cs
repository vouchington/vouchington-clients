using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinator
{
  private OAuthFailureSource? failureSource;

  private OAuthFailureSource? FailureSource
  {
    get => failureSource;
    set
    {
      if (failureSource == value) return;
      failureSource = value;
      OnPropertyChanged(nameof(CanRetryCapabilityLoading));
    }
  }

  public bool CanRetryCapabilityLoading =>
      FailureSource == OAuthFailureSource.Capabilities && Capabilities is null;

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Capability discovery is optional and must never terminate a MAUI page lifecycle.")]
  public async Task LoadCapabilitiesAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      var response = await client.FetchOAuthProvidersAsync(cancellationToken).ConfigureAwait(true);
      Capabilities = response.BrokerCapabilities;
      if (FailureSource == OAuthFailureSource.Operation) return;
      ErrorMessage = null;
      if (FailureSource == OAuthFailureSource.Capabilities &&
          Pending is null &&
          Result is null)
      {
        State = NativeOAuthAuthorizationState.Idle;
      }
      FailureSource = null;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      Capabilities = null;
      RecordFailure(ex.Message, OAuthFailureSource.Capabilities);
    }
  }

  private void RecordFailure(
      string message,
      OAuthFailureSource source = OAuthFailureSource.Operation)
  {
    ErrorMessage = message;
    FailureSource = source;
    State = NativeOAuthAuthorizationState.Failed;
  }

  private enum OAuthFailureSource
  {
    Capabilities,
    Operation,
  }
}

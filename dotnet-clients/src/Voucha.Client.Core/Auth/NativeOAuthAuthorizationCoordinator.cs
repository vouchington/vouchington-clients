using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinator :
    ObservableObject,
    ISupersededNativeOAuthAuthorizationDiscarder,
    IDisposable
{
  private readonly VouchaApiClient client;
  private readonly INativeOAuthAuthorizationPersistence persistence;
  private readonly INativeExternalBrowser browser;
  private readonly ISessionStore sessionStore;
  private readonly Func<DateTimeOffset> now;
  private readonly Func<TimeSpan, CancellationToken, Task> delay;
  private readonly SemaphoreSlim finalizationGate = new(1, 1);
  private readonly SemaphoreSlim disconnectionGate = new(1, 1);
  private NativeOAuthAuthorizationState state;
  private OAuthBrokerCapabilities? capabilities;
  private PendingNativeOAuthAuthorization? pending;
  private NativeOAuthAuthorizationResult? result;
  private string? errorMessage;

  public NativeOAuthAuthorizationCoordinator(
      VouchaApiClient client,
      INativeOAuthAuthorizationPersistence persistence,
      INativeExternalBrowser browser,
      ISessionStore sessionStore,
      Func<DateTimeOffset>? now = null,
      Func<TimeSpan, CancellationToken, Task>? delay = null)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    this.browser = browser ?? throw new ArgumentNullException(nameof(browser));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.now = now ?? (() => DateTimeOffset.UtcNow);
    this.delay = delay ?? Task.Delay;
  }

  public NativeOAuthAuthorizationState State
  {
    get => state;
    private set
    {
      if (value != NativeOAuthAuthorizationState.Failed) FailureSource = null;
      SetProperty(ref state, value);
    }
  }

  public OAuthBrokerCapabilities? Capabilities
  {
    get => capabilities;
    private set => SetProperty(ref capabilities, value);
  }

  public PendingNativeOAuthAuthorization? Pending
  {
    get => pending;
    private set => SetProperty(ref pending, value);
  }

  public NativeOAuthAuthorizationResult? Result
  {
    get => result;
    private set => SetProperty(ref result, value);
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  public bool CanCancelPendingAuthorization =>
      (Pending is not null &&
       State is NativeOAuthAuthorizationState.WaitingForCallback or NativeOAuthAuthorizationState.Failed) ||
      Result?.Kind is NativeOAuthAuthorizationResultKind.MfaRequired or
          NativeOAuthAuthorizationResultKind.Expired;

  public bool Supports(OAuthBrokerProvider provider, OAuthAuthorizationPurpose purpose) =>
      Capabilities?[provider].SupportsNative(purpose) == true;

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "OAuth start failures are presented by MAUI and must not escape async-void handlers.")]
  public async Task StartAsync(
      OAuthBrokerProvider provider,
      OAuthAuthorizationPurpose purpose,
      CancellationToken cancellationToken = default)
  {
    var discardGeneration = CaptureSuccessfulAuthenticationDiscardGeneration();
    if (!TryAcquireStartLease()) return;
    var pendingMayHaveBeenPersisted = false;
    try
    {
      await SynchronizeAsync(cancellationToken).ConfigureAwait(true);
      if (Pending is not null || Result is not null) return;
      if (Capabilities is null)
      {
        await LoadCapabilitiesAsync(cancellationToken).ConfigureAwait(true);
      }
      if (!Supports(provider, purpose)) return;

      State = NativeOAuthAuthorizationState.Connecting;
      ErrorMessage = null;
      var proof = NativeAuthorizationCompletionProof.Create();
      var response = await client.BeginOAuthAuthorizationAsync(
          provider,
          purpose,
          proof.Challenge,
          cancellationToken).ConfigureAwait(true);
      if (response.RedirectUrl.Scheme != Uri.UriSchemeHttps || response.ExpiresAt <= now())
      {
        throw new InvalidOperationException("OAuth authorization response is invalid.");
      }
      var authorization = new PendingNativeOAuthAuthorization(
          response.FlowId,
          provider,
          purpose,
          proof.Verifier,
          response.ExpiresAt);
      if (await StopInvalidatedStartAsync(discardGeneration, purpose, false).ConfigureAwait(true)) return;
      pendingMayHaveBeenPersisted = true;
      await persistence.WritePendingAsync(authorization, cancellationToken).ConfigureAwait(true);
      Pending = authorization;
      var browserOpen = TryOpenBrowserForCurrentStart(
          discardGeneration,
          purpose,
          response.RedirectUrl,
          cancellationToken);
      if (browserOpen is null)
      {
        _ = await StopInvalidatedStartAsync(discardGeneration, purpose, true).ConfigureAwait(true);
        return;
      }
      if (!await browserOpen.ConfigureAwait(true))
      {
        throw new InvalidOperationException("The system browser could not open.");
      }
      if (await StopInvalidatedStartAsync(discardGeneration, purpose, true).ConfigureAwait(true)) return;
      State = NativeOAuthAuthorizationState.WaitingForCallback;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _ = await ClearFailedStartAsync(pendingMayHaveBeenPersisted).ConfigureAwait(true);
      State = NativeOAuthAuthorizationState.Cancelled;
      throw;
    }
    catch (Exception ex)
    {
      var cleanupError = await ClearFailedStartAsync(pendingMayHaveBeenPersisted)
          .ConfigureAwait(true);
      var message = cleanupError is null
          ? ex.Message
          : string.Join(Environment.NewLine, ex.Message, cleanupError.Message);
      RecordFailure(message);
    }
    finally
    {
      ReleaseStartLease();
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Best-effort cleanup must preserve the original OAuth start outcome.")]
  private async Task<Exception?> ClearFailedStartAsync(bool pendingMayHaveBeenPersisted)
  {
    if (!pendingMayHaveBeenPersisted) return null;
    try
    {
      await persistence.ClearPendingAsync(CancellationToken.None).ConfigureAwait(true);
      Pending = null;
      return null;
    }
    catch (Exception ex)
    {
      return ex;
    }
  }

  public void Dispose()
  {
    DisposeStartLease();
    finalizationGate.Dispose();
    disconnectionGate.Dispose();
  }
}

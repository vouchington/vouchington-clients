using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationCoordinatorTests
{
  [Fact]
  public void CompletionProofMatchesBrokerContract()
  {
    var proof = NativeAuthorizationCompletionProof.Create();

    Assert.Equal(43, proof.Verifier.Length);
    Assert.Equal(43, proof.Challenge.Length);
    Assert.DoesNotContain('=', proof.Verifier);
    Assert.DoesNotContain('=', proof.Challenge);
    Assert.NotEqual(proof.Verifier, proof.Challenge);
  }

  [Fact]
  public async Task CallbackFinalizesOnceAndSupportsColdResume()
  {
    var handler = new RecordingHandler([
      Response("native.oauth.providers.broker-capabilities"),
      Response("native.oauth.authorization.begin"),
      Response("native.oauth.authorization.complete.authenticated"),
    ]);
    var persistence = new InMemoryPersistence();
    var browser = new RecordingBrowser(() => persistence.Pending is not null);
    var session = new RecordingSessionStore
    {
      RefreshSnapshot = new SessionSnapshot(new User("user-1", "testuser")),
    };
    using var coordinator = Coordinator(handler, persistence, browser, session);

    await coordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    var callback = new Uri(
        "voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token");
    Assert.True((await coordinator.HandleCallbackAsync(
        callback,
        TestContext.Current.CancellationToken)).Handled);
    Assert.True((await coordinator.HandleCallbackAsync(
        callback,
        TestContext.Current.CancellationToken)).Handled);

    Assert.True(browser.SawPersistedAuthorization);
    Assert.Null(persistence.Pending);
    Assert.Equal(NativeOAuthAuthorizationResultKind.Authenticated, persistence.Result?.Kind);
    Assert.Equal(1, session.RefreshCount);
    Assert.Equal(3, handler.Requests.Count);

    using var resumed = Coordinator(
        new RecordingHandler("{}"),
        persistence,
        new RecordingBrowser(() => true),
        session);
    await resumed.ResumeAsync(TestContext.Current.CancellationToken);
    Assert.Equal(NativeOAuthAuthorizationState.Authenticated, resumed.State);
  }

  [Fact]
  public async Task CompletionProducesMfaAndDurableExpiryResults()
  {
    var mfaPersistence = new InMemoryPersistence
    {
      Pending = Pending(completionToken: "native-completion-token"),
    };
    using var mfaCoordinator = Coordinator(
        new RecordingHandler(ApiFixtureLoader.LoadResponse(
            "native.oauth.authorization.complete.mfa")),
        mfaPersistence,
        new RecordingBrowser(() => true),
        new RecordingSessionStore());
    await mfaCoordinator.ResumeAsync(TestContext.Current.CancellationToken);
    Assert.Equal(NativeOAuthAuthorizationState.MfaRequired, mfaCoordinator.State);
    Assert.Equal(
        "019fafba-d1a5-7ec2-a8d7-f3895d7a465a",
        mfaCoordinator.Result?.LoginAttemptId);

    var expiry = new DateTimeOffset(2026, 7, 29, 22, 0, 0, TimeSpan.Zero);
    var expiredPersistence = new InMemoryPersistence { Pending = Pending(expiresAt: expiry) };
    using var expiredCoordinator = Coordinator(
        new RecordingHandler("{}"),
        expiredPersistence,
        new RecordingBrowser(() => true),
        new RecordingSessionStore(),
        expiry);

    await expiredCoordinator.ResumeAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Expired, expiredCoordinator.State);
    Assert.Equal(
        NativeOAuthAuthorizationResultKind.Expired,
        expiredPersistence.Result?.Kind);
    Assert.Null(expiredPersistence.Pending);

    using var finalizationCancellation = new CancellationTokenSource();
    using var cancelled = Coordinator(
        new CancellingHandler(finalizationCancellation),
        new InMemoryPersistence { Pending = Pending(completionToken: "native-completion-token") },
        new RecordingBrowser(() => true),
        new RecordingSessionStore());
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        cancelled.ResumeAsync(finalizationCancellation.Token));
    Assert.Equal(NativeOAuthAuthorizationState.WaitingForCallback, cancelled.State);
  }

  [Fact]
  public async Task CapabilityFailureClearsSupportWithoutEscapingPageLifecycles()
  {
    var handler = new RecordingHandler([
      Response("native.oauth.providers.broker-capabilities"),
      new RecordedResponse("""{"message":"offline"}""", HttpStatusCode.ServiceUnavailable),
    ]);
    using var coordinator = Coordinator(
        handler,
        new InMemoryPersistence(),
        new RecordingBrowser(() => true),
        new RecordingSessionStore());

    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    Assert.True(coordinator.Supports(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate));

    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);

    Assert.False(coordinator.Supports(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate));
    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.NotNull(coordinator.ErrorMessage);
  }

  [Fact]
  public async Task CapabilityTimeoutWithoutCallerCancellationRendersOAuthUnavailable()
  {
    using var coordinator = Coordinator(
        new ThrowingHandler(new TaskCanceledException("provider timeout")),
        new InMemoryPersistence(),
        new RecordingBrowser(() => true),
        new RecordingSessionStore());

    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);

    Assert.Null(coordinator.Capabilities);
    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Contains("provider timeout", coordinator.ErrorMessage, StringComparison.Ordinal);
  }

  [Fact]
  public async Task StartFailuresRemainInCoordinatorStateForMauiHandlers()
  {
    using var beginFailure = Coordinator(
        new RecordingHandler([
          Response("native.oauth.providers.broker-capabilities"),
          new RecordedResponse("""{"message":"offline"}""", HttpStatusCode.ServiceUnavailable),
        ]),
        new InMemoryPersistence(),
        new RecordingBrowser(() => true),
        new RecordingSessionStore());

    await beginFailure.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, beginFailure.State);
    Assert.NotNull(beginFailure.ErrorMessage);
    Assert.Null(beginFailure.Pending);

    var writeFailurePersistence = new InMemoryPersistence
    {
      WriteException = new InvalidOperationException("secure storage unavailable"),
      ClearException = new InvalidOperationException("secure cleanup unavailable"),
    };
    using var writeFailure = Coordinator(
        new RecordingHandler([
          Response("native.oauth.providers.broker-capabilities"),
          Response("native.oauth.authorization.begin"),
        ]),
        writeFailurePersistence,
        new RecordingBrowser(() => true),
        new RecordingSessionStore());

    await writeFailure.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, writeFailure.State);
    Assert.Contains("secure storage unavailable", writeFailure.ErrorMessage, StringComparison.Ordinal);
    Assert.Contains("secure cleanup unavailable", writeFailure.ErrorMessage, StringComparison.Ordinal);
    Assert.Equal(1, writeFailurePersistence.ClearCount);
    Assert.Null(writeFailure.Pending);

    var browserFailurePersistence = new InMemoryPersistence();
    using var browserFailure = Coordinator(
        new RecordingHandler([
          Response("native.oauth.providers.broker-capabilities"),
          Response("native.oauth.authorization.begin"),
        ]),
        browserFailurePersistence,
        new ThrowingBrowser(new InvalidOperationException("browser unavailable")),
        new RecordingSessionStore());

    await browserFailure.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, browserFailure.State);
    Assert.Contains("browser unavailable", browserFailure.ErrorMessage, StringComparison.Ordinal);
    Assert.Equal(1, browserFailurePersistence.ClearCount);
    Assert.Null(browserFailure.Pending);
    Assert.Null(browserFailurePersistence.Pending);
  }

  [Fact]
  public async Task StartPreservesCallerRequestedCancellation()
  {
    var handler = new RecordingHandler([
      Response("native.oauth.providers.broker-capabilities"),
      Response("native.oauth.authorization.begin"),
    ]);
    using var coordinator = Coordinator(
        handler,
        new InMemoryPersistence(),
        new RecordingBrowser(() => true),
        new RecordingSessionStore());
    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        coordinator.StartAsync(
            OAuthBrokerProvider.Github,
            OAuthAuthorizationPurpose.Authenticate,
            cancellation.Token));

    var persistedCancellation = new InMemoryPersistence();
    using var browserCancellation = new CancellationTokenSource();
    using var persistedCoordinator = Coordinator(
        new RecordingHandler([
          Response("native.oauth.providers.broker-capabilities"),
          Response("native.oauth.authorization.begin"),
        ]),
        persistedCancellation,
        new CancellingBrowser(browserCancellation),
        new RecordingSessionStore());

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        persistedCoordinator.StartAsync(
            OAuthBrokerProvider.Github,
            OAuthAuthorizationPurpose.Authenticate,
            browserCancellation.Token));

    Assert.Equal(1, persistedCancellation.ClearCount);
    Assert.Null(persistedCancellation.Pending);
    Assert.Null(persistedCoordinator.Pending);
    Assert.Equal(NativeOAuthAuthorizationState.Cancelled, persistedCoordinator.State);
  }

  [Fact]
  public async Task DisconnectFailuresRemainInCoordinatorStateForMauiHandlers()
  {
    using var requestFailure = Coordinator(
        new ThrowingHandler(new InvalidOperationException("disconnect unavailable")),
        new InMemoryPersistence(),
        new RecordingBrowser(() => true),
        new RecordingSessionStore());

    await requestFailure.DisconnectAsync(
        OAuthBrokerProvider.Github,
        TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, requestFailure.State);
    Assert.Contains("disconnect unavailable", requestFailure.ErrorMessage, StringComparison.Ordinal);

    using var refreshFailure = Coordinator(
        new RecordingHandler("{}"),
        new InMemoryPersistence(),
        new RecordingBrowser(() => true),
        new RecordingSessionStore
        {
          RefreshException = new InvalidOperationException("session refresh unavailable"),
        });

    await refreshFailure.DisconnectAsync(
        OAuthBrokerProvider.Github,
        TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, refreshFailure.State);
    Assert.Contains("session refresh unavailable", refreshFailure.ErrorMessage, StringComparison.Ordinal);
  }

  [Fact]
  public async Task DisconnectPreservesCallerRequestedCancellation()
  {
    using var cancellation = new CancellationTokenSource();
    using var coordinator = Coordinator(
        new CancellingHandler(cancellation),
        new InMemoryPersistence(),
        new RecordingBrowser(() => true),
        new RecordingSessionStore());

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        coordinator.DisconnectAsync(OAuthBrokerProvider.Github, cancellation.Token));

    Assert.Equal(NativeOAuthAuthorizationState.Idle, coordinator.State);
    Assert.Null(coordinator.ErrorMessage);
  }

  private static NativeOAuthAuthorizationCoordinator Coordinator(
      HttpMessageHandler handler,
      InMemoryPersistence persistence,
      INativeExternalBrowser browser,
      ISessionStore session,
      DateTimeOffset? now = null) =>
      new(
          new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
          persistence,
          browser,
          session,
          () => now ?? new DateTimeOffset(2026, 7, 29, 21, 0, 0, TimeSpan.Zero),
          (_, _) => Task.CompletedTask);

  private static RecordedResponse Response(string fixtureId) =>
      new(ApiFixtureLoader.LoadResponse(fixtureId), HttpStatusCode.OK);

  private static PendingNativeOAuthAuthorization Pending(
      string? completionToken = null,
      DateTimeOffset? expiresAt = null) =>
      new(
          "019fafb8-a44c-73e2-890a-497ff3dd27a6",
          OAuthBrokerProvider.Github,
          OAuthAuthorizationPurpose.Authenticate,
          "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
          expiresAt ?? new DateTimeOffset(2026, 7, 29, 22, 0, 0, TimeSpan.Zero),
          completionToken);

  private sealed class RecordingBrowser(Func<bool> persistedCheck) : INativeExternalBrowser
  {
    public bool SawPersistedAuthorization { get; private set; }

    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
      SawPersistedAuthorization = persistedCheck();
      return Task.FromResult(uri.Scheme == Uri.UriSchemeHttps);
    }
  }

  private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(exception);
  }

  private sealed class CancellingHandler(CancellationTokenSource cancellation) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      cancellation.Cancel();
      return Task.FromCanceled<HttpResponseMessage>(cancellation.Token);
    }
  }

  private sealed class ThrowingBrowser(Exception exception) : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        Task.FromException<bool>(exception);
  }

  private sealed class CancellingBrowser(CancellationTokenSource cancellation) : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
      cancellation.Cancel();
      return Task.FromCanceled<bool>(cancellation.Token);
    }
  }

  private sealed class RecordingSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; private set; } = SessionSnapshot.Anonymous;
    public SessionSnapshot? RefreshSnapshot { get; init; }
    public Exception? RefreshException { get; init; }
    public int RefreshCount { get; private set; }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
      RefreshCount++;
      if (RefreshException is not null)
      {
        return Task.FromException(RefreshException);
      }
      if (RefreshSnapshot is not null)
      {
        Current = RefreshSnapshot;
        SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
      }
      return Task.CompletedTask;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
      Current = SessionSnapshot.Anonymous;
      return Task.CompletedTask;
    }
  }

  private sealed class InMemoryPersistence : INativeOAuthAuthorizationPersistence
  {
    public PendingNativeOAuthAuthorization? Pending { get; set; }
    public NativeOAuthAuthorizationResult? Result { get; set; }
    public Exception? WriteException { get; init; }
    public Exception? ClearException { get; init; }
    public int ClearCount { get; private set; }

    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NativeOAuthAuthorizationSnapshot(Pending, Result));

    public Task WritePendingAsync(
        PendingNativeOAuthAuthorization pending,
        CancellationToken cancellationToken = default)
    {
      if (WriteException is not null)
      {
        return Task.FromException(WriteException);
      }
      Pending = pending;
      Result = null;
      return Task.CompletedTask;
    }

    public Task<bool> ClaimCallbackAsync(
        string flowId,
        string completionToken,
        CancellationToken cancellationToken = default)
    {
      if (Pending is null ||
          Pending.CompletionToken is not null ||
          !StringComparer.Ordinal.Equals(Pending.FlowId, flowId))
      {
        return Task.FromResult(false);
      }
      Pending = Pending with { CompletionToken = completionToken };
      return Task.FromResult(true);
    }

    public Task<bool> CompleteAsync(
        string flowId,
        NativeOAuthAuthorizationResult result,
        CancellationToken cancellationToken = default)
    {
      if (Pending is null || !StringComparer.Ordinal.Equals(Pending.FlowId, flowId))
      {
        return Task.FromResult(false);
      }
      Pending = null;
      Result = result;
      return Task.FromResult(true);
    }

    public Task ClearPendingAsync(CancellationToken cancellationToken = default)
    {
      ClearCount++;
      if (ClearException is not null)
      {
        return Task.FromException(ClearException);
      }
      Pending = null;
      return Task.CompletedTask;
    }

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default)
    {
      Result = null;
      return Task.CompletedTask;
    }
  }
}

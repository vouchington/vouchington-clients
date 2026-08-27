using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationStartConcurrencyTests
{
  [Fact]
  public async Task ConcurrentStartsIssueSingleAuthorizationBeforePersistenceReadCompletes()
  {
    var persistence = new ControlledPersistence(blockFirstRead: true);
    var handler = Handler(
        Response("native.oauth.providers.broker-capabilities"),
        Response("native.oauth.authorization.begin"));
    using var coordinator = Coordinator(handler, persistence);

    var first = coordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    await persistence.FirstReadEntered.WaitAsync(TestContext.Current.CancellationToken);

    await coordinator.StartAsync(
        OAuthBrokerProvider.Facebook,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    persistence.ReleaseFirstRead();
    await first;

    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal(1, persistence.WriteCount);
    Assert.Equal(OAuthBrokerProvider.Github, persistence.Pending?.Provider);
    Assert.Equal(NativeOAuthAuthorizationState.WaitingForCallback, coordinator.State);
  }

  [Fact]
  public async Task CancellationAndFailureReleaseTheStartLeaseForRetry()
  {
    using var cancellation = new CancellationTokenSource();
    var cancelledPersistence = new ControlledPersistence(blockFirstRead: true);
    var cancelledHandler = Handler(
        Response("native.oauth.providers.broker-capabilities"),
        Response("native.oauth.authorization.begin"));
    using var cancelledCoordinator = Coordinator(cancelledHandler, cancelledPersistence);

    var cancelledStart = cancelledCoordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        cancellation.Token);
    await cancelledPersistence.FirstReadEntered.WaitAsync(TestContext.Current.CancellationToken);
    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledStart);

    await cancelledCoordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    Assert.Equal(1, cancelledPersistence.WriteCount);

    var failedPersistence = new ControlledPersistence(
        firstReadException: new InvalidOperationException("secure read failed"));
    var failedHandler = Handler(
        Response("native.oauth.providers.broker-capabilities"),
        Response("native.oauth.authorization.begin"));
    using var failedCoordinator = Coordinator(failedHandler, failedPersistence);

    await failedCoordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    Assert.Equal(NativeOAuthAuthorizationState.Failed, failedCoordinator.State);

    await failedCoordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    Assert.Equal(1, failedPersistence.WriteCount);
    Assert.Equal(NativeOAuthAuthorizationState.WaitingForCallback, failedCoordinator.State);
  }

  [Fact]
  public async Task DisposalDuringStartPreservesTheLeaseAndRejectsFutureStarts()
  {
    var persistence = new ControlledPersistence(blockFirstRead: true);
    var coordinator = Coordinator(
        Handler(
            Response("native.oauth.providers.broker-capabilities"),
            Response("native.oauth.authorization.begin")),
        persistence);

    var activeStart = coordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    await persistence.FirstReadEntered.WaitAsync(TestContext.Current.CancellationToken);
    coordinator.Dispose();
    persistence.ReleaseFirstRead();
    await activeStart;

    await Assert.ThrowsAsync<ObjectDisposedException>(() => coordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken));
    Assert.Equal(1, persistence.WriteCount);
  }

  private static NativeOAuthAuthorizationCoordinator Coordinator(
      RecordingHandler handler,
      INativeOAuthAuthorizationPersistence persistence) =>
      new(
          new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
          persistence,
          new SuccessfulBrowser(),
          new NoopSessionStore(),
          () => new DateTimeOffset(2026, 7, 29, 21, 0, 0, TimeSpan.Zero),
          (_, _) => Task.CompletedTask);

  private static RecordingHandler Handler(params RecordedResponse[] responses) => new(responses);

  private static RecordedResponse Response(string fixtureId) =>
      new(ApiFixtureLoader.LoadResponse(fixtureId), HttpStatusCode.OK);

  private sealed class ControlledPersistence(
      bool blockFirstRead = false,
      Exception? firstReadException = null) : INativeOAuthAuthorizationPersistence
  {
    private readonly TaskCompletionSource firstReadEntered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource firstReadRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int readCount;

    public Task FirstReadEntered => firstReadEntered.Task;
    public PendingNativeOAuthAuthorization? Pending { get; private set; }
    public int WriteCount { get; private set; }

    public void ReleaseFirstRead() => firstReadRelease.TrySetResult();

    public async Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default)
    {
      if (Interlocked.Increment(ref readCount) == 1)
      {
        firstReadEntered.TrySetResult();
        if (firstReadException is not null) throw firstReadException;
        if (blockFirstRead)
        {
          await firstReadRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
      }
      return new NativeOAuthAuthorizationSnapshot(Pending, null);
    }

    public Task WritePendingAsync(
        PendingNativeOAuthAuthorization pending,
        CancellationToken cancellationToken = default)
    {
      WriteCount++;
      Pending = pending;
      return Task.CompletedTask;
    }

    public Task<bool> ClaimCallbackAsync(
        string flowId,
        string completionToken,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> CompleteAsync(
        string flowId,
        NativeOAuthAuthorizationResult result,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task ClearPendingAsync(CancellationToken cancellationToken = default)
    {
      Pending = null;
      return Task.CompletedTask;
    }

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }

  private sealed class SuccessfulBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
  }

  private sealed class NoopSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current => SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

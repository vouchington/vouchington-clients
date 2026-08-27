using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationCancellationFailureTests
{
  [Fact]
  public async Task PendingClearFailureRemainsRecoverable()
  {
    var pending = new PendingNativeOAuthAuthorization(
        "019fafb8-a44c-73e2-890a-497ff3dd27a6",
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
        DateTimeOffset.UtcNow.AddMinutes(5));
    var persistence = new FailingCancellationPersistence
    {
      Pending = pending,
      ClearException = new InvalidOperationException("secure pending clear unavailable"),
    };
    using var coordinator = Coordinator(persistence);
    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);

    await coordinator.CancelAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Contains("secure pending clear unavailable", coordinator.ErrorMessage);
    Assert.Equal(pending, coordinator.Pending);
    Assert.Equal(pending, persistence.Pending);
    Assert.True(coordinator.CanCancelPendingAuthorization);

    persistence.ClearException = null;
    await coordinator.CancelAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Cancelled, coordinator.State);
    Assert.Null(coordinator.Pending);
    Assert.Null(persistence.Pending);
    Assert.Null(coordinator.ErrorMessage);
  }

  [Fact]
  public async Task ResultAcknowledgementFailureRemainsRecoverable()
  {
    var result = new NativeOAuthAuthorizationResult(
        NativeOAuthAuthorizationResultKind.MfaRequired,
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        "oauth-mfa-attempt");
    var persistence = new FailingCancellationPersistence
    {
      Result = result,
      AcknowledgeException = new InvalidOperationException("secure result clear unavailable"),
    };
    using var coordinator = Coordinator(persistence);
    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);

    await coordinator.CancelAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Contains("secure result clear unavailable", coordinator.ErrorMessage);
    Assert.Equal(result, coordinator.Result);
    Assert.Equal(result, persistence.Result);
    Assert.True(coordinator.CanCancelPendingAuthorization);

    persistence.AcknowledgeException = null;
    await coordinator.CancelAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Cancelled, coordinator.State);
    Assert.Null(coordinator.Result);
    Assert.Null(persistence.Result);
    Assert.Null(coordinator.ErrorMessage);
  }

  [Fact]
  public async Task DirectResultAcknowledgementFailureRemainsRecoverable()
  {
    var result = new NativeOAuthAuthorizationResult(
        NativeOAuthAuthorizationResultKind.Connected,
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Connect);
    var persistence = new FailingCancellationPersistence
    {
      Result = result,
      AcknowledgeException = new InvalidOperationException("secure result clear unavailable"),
    };
    using var coordinator = Coordinator(persistence);
    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);

    await coordinator.AcknowledgeResultAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Contains("secure result clear unavailable", coordinator.ErrorMessage);
    Assert.Equal(result, coordinator.Result);
    Assert.Equal(result, persistence.Result);

    persistence.AcknowledgeException = null;
    await coordinator.AcknowledgeResultAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Idle, coordinator.State);
    Assert.Null(coordinator.Result);
    Assert.Null(persistence.Result);
    Assert.Null(coordinator.ErrorMessage);
  }

  [Fact]
  public async Task DirectResultAcknowledgementCallerCancellationStillPropagates()
  {
    var result = new NativeOAuthAuthorizationResult(
        NativeOAuthAuthorizationResultKind.Connected,
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Connect);
    var persistence = new FailingCancellationPersistence { Result = result };
    using var coordinator = Coordinator(persistence);
    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    persistence.AcknowledgeException = new OperationCanceledException(cancellation.Token);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        coordinator.AcknowledgeResultAsync(cancellation.Token));

    Assert.Equal(NativeOAuthAuthorizationState.Connected, coordinator.State);
    Assert.Equal(result, coordinator.Result);
    Assert.Equal(result, persistence.Result);
  }

  [Fact]
  public async Task CallerRequestedCancellationStillPropagates()
  {
    var pending = new PendingNativeOAuthAuthorization(
        "019fafb8-a44c-73e2-890a-497ff3dd27a6",
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
        DateTimeOffset.UtcNow.AddMinutes(5));
    var persistence = new FailingCancellationPersistence { Pending = pending };
    using var coordinator = Coordinator(persistence);
    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    persistence.ClearException = new OperationCanceledException(cancellation.Token);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        coordinator.CancelAsync(cancellation.Token));

    Assert.Equal(NativeOAuthAuthorizationState.WaitingForCallback, coordinator.State);
    Assert.Equal(pending, coordinator.Pending);
    Assert.Equal(pending, persistence.Pending);
  }

  private static NativeOAuthAuthorizationCoordinator Coordinator(
      INativeOAuthAuthorizationPersistence persistence) =>
      new(
          new VouchaApiClient(new HttpClient(new UnusedHandler())
          {
            BaseAddress = new Uri("https://api.test"),
          }),
          persistence,
          new UnusedBrowser(),
          new AnonymousSessionStore());

  private sealed class FailingCancellationPersistence :
      INativeOAuthAuthorizationPersistence
  {
    public PendingNativeOAuthAuthorization? Pending { get; set; }
    public NativeOAuthAuthorizationResult? Result { get; set; }
    public Exception? ClearException { get; set; }
    public Exception? AcknowledgeException { get; set; }

    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NativeOAuthAuthorizationSnapshot(Pending, Result));

    public Task WritePendingAsync(
        PendingNativeOAuthAuthorization pending,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> ClaimCallbackAsync(
        string flowId,
        string completionToken,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> CompleteAsync(
        string flowId,
        NativeOAuthAuthorizationResult result,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task ClearPendingAsync(CancellationToken cancellationToken = default)
    {
      if (ClearException is not null) return Task.FromException(ClearException);
      Pending = null;
      return Task.CompletedTask;
    }

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default)
    {
      if (AcknowledgeException is not null)
      {
        return Task.FromException(AcknowledgeException);
      }
      Result = null;
      return Task.CompletedTask;
    }
  }

  private sealed class UnusedHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
  }

  private sealed class UnusedBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current => SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }
}

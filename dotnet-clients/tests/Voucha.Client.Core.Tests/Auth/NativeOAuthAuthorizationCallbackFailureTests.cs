using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationCallbackFailureTests
{
  [Fact]
  public async Task ResumeStorageReadFailureBecomesSafeFailedState()
  {
    using var coordinator = Coordinator(new FailingCallbackPersistence(
        failClaim: false,
        "secure resume read unavailable"));

    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Contains(
        "secure resume read unavailable",
        coordinator.ErrorMessage,
        StringComparison.Ordinal);
  }

  [Fact]
  public async Task ResumePreservesCallerCancellation()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    using var coordinator = Coordinator(new FailingCallbackPersistence(
        failClaim: false,
        "unused",
        cancellation.Token));

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        coordinator.ResumeAsync(cancellation.Token));
  }

  [Theory]
  [InlineData(true, "secure callback write unavailable")]
  [InlineData(false, "secure callback read unavailable")]
  public async Task StorageFailuresBecomeRecoverableCallbackState(
      bool failClaim,
      string expectedError)
  {
    var persistence = new FailingCallbackPersistence(failClaim, expectedError);
    using var coordinator = Coordinator(persistence);

    var handled = await coordinator.HandleCallbackAsync(
        new Uri(
            "voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token"),
        TestContext.Current.CancellationToken);

    Assert.True(handled.Handled);
    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Contains(expectedError, coordinator.ErrorMessage, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CallerCancellationStillPropagates()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    using var coordinator = Coordinator(new FailingCallbackPersistence(
        failClaim: true,
        "unused",
        cancellation.Token));

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        coordinator.HandleCallbackAsync(
            new Uri(
                "voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token"),
            cancellation.Token));
  }

  [Fact]
  public async Task FailedCallbackWithPendingAuthorizationCanBeCancelled()
  {
    var persistence = new FailingCallbackPersistence(
        failClaim: true,
        "secure callback write unavailable")
    {
      Pending = PendingAuthorization(),
    };
    using var coordinator = Coordinator(persistence);
    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);

    await coordinator.HandleCallbackAsync(Callback(), TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.True(coordinator.CanCancelPendingAuthorization);
    await coordinator.CancelAsync(TestContext.Current.CancellationToken);
    Assert.Equal(NativeOAuthAuthorizationState.Cancelled, coordinator.State);
    Assert.False(coordinator.CanCancelPendingAuthorization);
  }

  private static Uri Callback() =>
      new(
          "voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token");

  private static PendingNativeOAuthAuthorization PendingAuthorization() =>
      new(
          "019fafb8-a44c-73e2-890a-497ff3dd27a6",
          OAuthBrokerProvider.Github,
          OAuthAuthorizationPurpose.Authenticate,
          "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
          DateTimeOffset.UtcNow.AddMinutes(5));

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

  private sealed class FailingCallbackPersistence(
      bool failClaim,
      string error,
      CancellationToken cancelledToken = default) : INativeOAuthAuthorizationPersistence
  {
    public PendingNativeOAuthAuthorization? Pending { get; set; }

    public Task<bool> ClaimCallbackAsync(
        string flowId,
        string completionToken,
        CancellationToken cancellationToken = default) =>
        cancelledToken.IsCancellationRequested
            ? Task.FromCanceled<bool>(cancelledToken)
            : failClaim
                ? Task.FromException<bool>(new InvalidOperationException(error))
                : Task.FromResult(false);

    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default) =>
        cancelledToken.IsCancellationRequested
            ? Task.FromCanceled<NativeOAuthAuthorizationSnapshot>(cancelledToken)
            : failClaim
                ? Task.FromResult(new NativeOAuthAuthorizationSnapshot(Pending, null))
                : Task.FromException<NativeOAuthAuthorizationSnapshot>(
                    new InvalidOperationException(error));

    public Task WritePendingAsync(
        PendingNativeOAuthAuthorization pending,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> CompleteAsync(
        string flowId,
        NativeOAuthAuthorizationResult result,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task ClearPendingAsync(CancellationToken cancellationToken = default)
    {
      Pending = null;
      return Task.CompletedTask;
    }

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }

  private sealed class UnusedHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(
            new InvalidOperationException("The callback must not reach the API after storage fails."));
  }

  private sealed class UnusedBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
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

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationMfaDurabilityTests
{
  [Fact]
  public async Task MfaResultSurvivesRestartUntilAuthenticationOrExplicitCancel()
  {
    var persistence = new MfaPersistence
    {
      Result = MfaResult(),
    };
    var session = new MutableSessionStore();
    using (var resumed = Coordinator(persistence, session))
    {
      await resumed.ResumeAsync(TestContext.Current.CancellationToken);

      Assert.Equal(NativeOAuthAuthorizationState.MfaRequired, resumed.State);
      Assert.True(resumed.CanCancelPendingAuthorization);
      await resumed.AcknowledgeResultAsync(TestContext.Current.CancellationToken);
      Assert.NotNull(persistence.Result);
    }

    using (var restarted = Coordinator(persistence, session))
    {
      await restarted.ResumeAsync(TestContext.Current.CancellationToken);
      Assert.Equal("oauth-mfa-attempt", restarted.Result?.LoginAttemptId);

      session.Authenticate();
      await restarted.AcknowledgeResultAsync(TestContext.Current.CancellationToken);

      Assert.Null(restarted.Result);
      Assert.Null(persistence.Result);
      Assert.Equal(NativeOAuthAuthorizationState.Idle, restarted.State);
    }

    persistence.Result = MfaResult();
    using var cancelled = Coordinator(persistence, new MutableSessionStore());
    await cancelled.ResumeAsync(TestContext.Current.CancellationToken);
    await cancelled.CancelAsync(TestContext.Current.CancellationToken);

    Assert.Null(cancelled.Result);
    Assert.Null(persistence.Result);
    Assert.Equal(NativeOAuthAuthorizationState.Cancelled, cancelled.State);
  }

  private static NativeOAuthAuthorizationResult MfaResult() =>
      new(
          NativeOAuthAuthorizationResultKind.MfaRequired,
          OAuthBrokerProvider.Github,
          OAuthAuthorizationPurpose.Authenticate,
          "oauth-mfa-attempt");

  private static NativeOAuthAuthorizationCoordinator Coordinator(
      INativeOAuthAuthorizationPersistence persistence,
      ISessionStore sessionStore) =>
      new(
          new VouchaApiClient(new HttpClient(new UnusedHandler())
          {
            BaseAddress = new Uri("https://api.test"),
          }),
          persistence,
          new UnusedBrowser(),
          sessionStore);

  private sealed class MfaPersistence : INativeOAuthAuthorizationPersistence
  {
    public NativeOAuthAuthorizationResult? Result { get; set; }

    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NativeOAuthAuthorizationSnapshot(null, Result));

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

    public Task ClearPendingAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default)
    {
      Result = null;
      return Task.CompletedTask;
    }
  }

  private sealed class MutableSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; private set; } = SessionSnapshot.Anonymous;

    public void Authenticate()
    {
      Current = new SessionSnapshot(new User("user-1", "oauth-user"));
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
      Current = SessionSnapshot.Anonymous;
      return Task.CompletedTask;
    }
  }

  private sealed class UnusedBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class UnusedHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
  }
}

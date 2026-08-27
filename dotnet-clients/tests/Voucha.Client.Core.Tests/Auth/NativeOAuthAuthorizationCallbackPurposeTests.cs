using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationCallbackPurposeTests
{
  [Fact]
  public async Task ExpiredResultRemainsDismissibleUntilExplicitCancellation()
  {
    var persistence = new CallbackPersistence
    {
      Result = new(
          NativeOAuthAuthorizationResultKind.Expired,
          OAuthBrokerProvider.Github,
          OAuthAuthorizationPurpose.Authenticate),
    };
    using var coordinator = Coordinator(persistence);

    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Expired, coordinator.State);
    Assert.True(coordinator.CanCancelPendingAuthorization);
    await coordinator.CancelAsync(TestContext.Current.CancellationToken);
    Assert.Equal(NativeOAuthAuthorizationState.Cancelled, coordinator.State);
    Assert.Null(persistence.Result);
  }

  [Fact]
  public async Task RejectedExpiryCompletionRestoresReplacementAuthorization()
  {
    var expired = PendingAuthorization("expired-flow", DateTimeOffset.UtcNow.AddMinutes(-1));
    var replacement = PendingAuthorization("replacement-flow", DateTimeOffset.UtcNow.AddMinutes(5));
    var persistence = new CallbackPersistence
    {
      Pending = expired,
      ReplacementOnComplete = replacement,
    };
    using var coordinator = Coordinator(persistence);

    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);

    Assert.Equal(replacement, coordinator.Pending);
    Assert.Null(coordinator.Result);
    Assert.Equal(NativeOAuthAuthorizationState.WaitingForCallback, coordinator.State);
  }

  [Fact]
  public async Task CallbackReturnsClaimedPurposeAfterResultIsAcknowledgedByObserver()
  {
    var persistence = new CallbackPersistence
    {
      Pending = new(
          "019fafb8-a44c-73e2-890a-497ff3dd27a6",
          OAuthBrokerProvider.Github,
          OAuthAuthorizationPurpose.Connect,
          "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
          DateTimeOffset.UtcNow.AddMinutes(5)),
    };
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(new ConnectedHandler())
        {
          BaseAddress = new Uri("https://api.test"),
        }),
        persistence,
        new UnusedBrowser(),
        new ConnectedSessionStore());
    coordinator.PropertyChanged += (_, eventArgs) =>
    {
      if (eventArgs.PropertyName == nameof(coordinator.Result) && coordinator.Result is not null)
      {
        coordinator.AcknowledgeResultAsync(TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();
      }
    };

    var outcome = await coordinator.HandleCallbackAsync(
        new Uri(
            "voucha://auth/oauth/callback?flow_id=019fafb8-a44c-73e2-890a-497ff3dd27a6&completion_token=native-completion-token"),
        TestContext.Current.CancellationToken);

    Assert.True(outcome.Handled);
    Assert.Equal(OAuthAuthorizationPurpose.Connect, outcome.Purpose);
    Assert.Null(coordinator.Result);
    Assert.Null(coordinator.Pending);
  }

  private sealed class ConnectedHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(
              """{"oauth_account":{"id":"github-user","name":"GitHub User"}}"""),
        });
  }

  private sealed class CallbackPersistence : INativeOAuthAuthorizationPersistence
  {
    public PendingNativeOAuthAuthorization? Pending { get; set; }
    public NativeOAuthAuthorizationResult? Result { get; set; }
    public PendingNativeOAuthAuthorization? ReplacementOnComplete { get; init; }

    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NativeOAuthAuthorizationSnapshot(Pending, Result));

    public Task WritePendingAsync(
        PendingNativeOAuthAuthorization pending,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<bool> ClaimCallbackAsync(
        string flowId,
        string completionToken,
        CancellationToken cancellationToken = default)
    {
      if (Pending?.FlowId != flowId) return Task.FromResult(false);
      Pending = Pending with { CompletionToken = completionToken };
      return Task.FromResult(true);
    }

    public Task<bool> CompleteAsync(
        string flowId,
        NativeOAuthAuthorizationResult result,
        CancellationToken cancellationToken = default)
    {
      if (Pending?.FlowId != flowId) return Task.FromResult(false);
      if (ReplacementOnComplete is not null)
      {
        Pending = ReplacementOnComplete;
        return Task.FromResult(false);
      }
      Pending = null;
      Result = result;
      return Task.FromResult(true);
    }

    public Task ClearPendingAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default)
    {
      Result = null;
      return Task.CompletedTask;
    }
  }

  private sealed class UnusedBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class ConnectedSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; } = new(new User(
        "user-1",
        "oauth-user",
        GithubAccount: new("github-user", "GitHub User")));

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }

  private static NativeOAuthAuthorizationCoordinator Coordinator(
      INativeOAuthAuthorizationPersistence persistence) =>
      new(
          new VouchaApiClient(new HttpClient(new ConnectedHandler())
          {
            BaseAddress = new Uri("https://api.test"),
          }),
          persistence,
          new UnusedBrowser(),
          new ConnectedSessionStore());

  private static PendingNativeOAuthAuthorization PendingAuthorization(
      string flowId,
      DateTimeOffset expiresAt) =>
      new(
          flowId,
          OAuthBrokerProvider.Github,
          OAuthAuthorizationPurpose.Authenticate,
          "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
          expiresAt);
}

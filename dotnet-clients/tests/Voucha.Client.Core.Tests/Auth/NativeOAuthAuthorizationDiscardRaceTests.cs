using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationDiscardRaceTests
{
  [Fact]
  public async Task SuccessfulOtherAuthenticationFencesLateBrokerStart()
  {
    var persistence = new RacePersistence();
    var handler = new BlockingBeginHandler();
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        persistence,
        new UnusedBrowser(),
        new AnonymousSessionStore(),
        () => new DateTimeOffset(2026, 7, 29, 21, 0, 0, TimeSpan.Zero));

    var start = coordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    await handler.BeginStarted.WaitAsync(TestContext.Current.CancellationToken);

    await coordinator.DiscardAfterSuccessfulAuthenticationAsync();
    handler.CompleteBegin();
    await start;

    Assert.Equal(0, persistence.WriteCount);
    Assert.Null(persistence.Pending);
    Assert.Null(coordinator.Pending);
    Assert.Equal(NativeOAuthAuthorizationState.Idle, coordinator.State);
  }

  [Fact]
  public async Task SuccessfulOtherAuthenticationFencesStartAwaitingBrowser()
  {
    var persistence = new RacePersistence();
    var browser = new BlockingBrowser();
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(new RecordingHandler([
          new RecordedResponse(
              ApiFixtureLoader.LoadResponse("native.oauth.providers.broker-capabilities"),
              HttpStatusCode.OK),
          new RecordedResponse(
              ApiFixtureLoader.LoadResponse("native.oauth.authorization.begin"),
              HttpStatusCode.OK),
        ]))
        {
          BaseAddress = new Uri("https://api.test"),
        }),
        persistence,
        browser,
        new AnonymousSessionStore(),
        () => new DateTimeOffset(2026, 7, 29, 21, 0, 0, TimeSpan.Zero));

    var start = coordinator.StartAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        TestContext.Current.CancellationToken);
    await browser.OpenStarted.WaitAsync(TestContext.Current.CancellationToken);

    await coordinator.DiscardAfterSuccessfulAuthenticationAsync();
    browser.CompleteOpen();
    await start;

    Assert.Equal(1, persistence.WriteCount);
    Assert.Null(persistence.Pending);
    Assert.Null(coordinator.Pending);
    Assert.Equal(NativeOAuthAuthorizationState.Idle, coordinator.State);
  }

  [Fact]
  public async Task SuccessfulOtherAuthenticationFencesLateBrokerFinalization()
  {
    var persistence = new RacePersistence
    {
      Pending = new PendingNativeOAuthAuthorization(
          "flow-1",
          OAuthBrokerProvider.Github,
          OAuthAuthorizationPurpose.Authenticate,
          "verifier",
          DateTimeOffset.UtcNow.AddMinutes(5),
          "completion-token"),
    };
    var session = new BlockingSessionStore();
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(new AuthenticatedCompletionHandler())
        {
          BaseAddress = new Uri("https://api.test"),
        }),
        persistence,
        new UnusedBrowser(),
        session);

    var finalization = coordinator.ResumeAsync(TestContext.Current.CancellationToken);
    await session.RefreshStarted.WaitAsync(TestContext.Current.CancellationToken);

    await coordinator.DiscardAfterSuccessfulAuthenticationAsync();
    session.CompleteRefresh();
    await finalization;

    Assert.Null(persistence.Pending);
    Assert.Null(persistence.Result);
    Assert.Null(coordinator.Pending);
    Assert.Null(coordinator.Result);
    Assert.Equal(NativeOAuthAuthorizationState.Idle, coordinator.State);
  }

  private sealed class BlockingSessionStore : ISessionStore
  {
    private readonly TaskCompletionSource refreshStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource refreshCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; private set; } = SessionSnapshot.Anonymous;
    public Task RefreshStarted => refreshStarted.Task;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
      refreshStarted.SetResult();
      await refreshCompletion.Task.WaitAsync(cancellationToken);
      Current = new SessionSnapshot(new User("user-1", "oauth-user"));
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }

    public void CompleteRefresh() => refreshCompletion.SetResult();

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }

  private sealed class RacePersistence : INativeOAuthAuthorizationPersistence
  {
    private readonly object gate = new();
    public PendingNativeOAuthAuthorization? Pending { get; set; }
    public NativeOAuthAuthorizationResult? Result { get; private set; }
    public int WriteCount { get; private set; }

    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default)
    {
      lock (gate)
      {
        return Task.FromResult(new NativeOAuthAuthorizationSnapshot(Pending, Result));
      }
    }

    public Task WritePendingAsync(
        PendingNativeOAuthAuthorization pending,
        CancellationToken cancellationToken = default)
    {
      lock (gate)
      {
        WriteCount++;
        Pending = pending;
      }
      return Task.CompletedTask;
    }

    public Task<bool> ClaimCallbackAsync(
        string flowId,
        string completionToken,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> CompleteAsync(
        string flowId,
        NativeOAuthAuthorizationResult result,
        CancellationToken cancellationToken = default)
    {
      lock (gate)
      {
        if (Pending?.FlowId != flowId) return Task.FromResult(false);
        Pending = null;
        Result = result;
        return Task.FromResult(true);
      }
    }

    public Task ClearPendingAsync(CancellationToken cancellationToken = default)
    {
      lock (gate) Pending = null;
      return Task.CompletedTask;
    }

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default)
    {
      lock (gate) Result = null;
      return Task.CompletedTask;
    }
  }

  private sealed class AuthenticatedCompletionHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(
              """{"user":{"id":"user-1","username":"oauth-user","roles":["user"]}}"""),
          RequestMessage = request,
        });
  }

  private sealed class BlockingBeginHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource beginStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource beginCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task BeginStarted => beginStarted.Task;

    public void CompleteBegin() => beginCompletion.SetResult();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var fixtureId = request.RequestUri?.AbsolutePath.EndsWith(
          "/authorizations",
          StringComparison.Ordinal) == true
          ? "native.oauth.authorization.begin"
          : "native.oauth.providers.broker-capabilities";
      if (fixtureId == "native.oauth.authorization.begin")
      {
        beginStarted.SetResult();
        await beginCompletion.Task.WaitAsync(cancellationToken);
      }
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(ApiFixtureLoader.LoadResponse(fixtureId)),
        RequestMessage = request,
      };
    }
  }

  private sealed class BlockingBrowser : INativeExternalBrowser
  {
    private readonly TaskCompletionSource openStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource openCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task OpenStarted => openStarted.Task;

    public void CompleteOpen() => openCompletion.SetResult();

    public async Task<bool> OpenAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
      openStarted.SetResult();
      await openCompletion.Task.WaitAsync(cancellationToken);
      return true;
    }
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }

    public SessionSnapshot Current => SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }

  private sealed class UnusedBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}

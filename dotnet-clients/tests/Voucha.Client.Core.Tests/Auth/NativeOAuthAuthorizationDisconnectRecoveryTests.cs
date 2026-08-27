using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationDisconnectRecoveryTests
{
  [Fact]
  public async Task CommittedDisconnectProjectsLocallyAndRetriesOnlyRefresh()
  {
    var handler = new DisconnectHandler();
    var session = new FailingRefreshSessionStore();
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        new UnusedPersistence(),
        new UnusedBrowser(),
        session);

    await coordinator.DisconnectAsync(
        OAuthBrokerProvider.Github,
        TestContext.Current.CancellationToken);

    Assert.False(coordinator.IsProviderConnected(OAuthBrokerProvider.Github));
    Assert.Null(session.Current.Identity?.GithubAccount);
    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Contains("session refresh unavailable", coordinator.ErrorMessage, StringComparison.Ordinal);

    await coordinator.DisconnectAsync(
        OAuthBrokerProvider.Github,
        TestContext.Current.CancellationToken);

    Assert.Equal(1, handler.RequestCount);
    Assert.Equal(2, session.RefreshCount);

    session.FailRefresh = false;
    await coordinator.DisconnectAsync(
        OAuthBrokerProvider.Github,
        TestContext.Current.CancellationToken);
    session.RestoreConnectedIdentity();

    Assert.Equal(NativeOAuthAuthorizationState.Idle, coordinator.State);
    Assert.True(coordinator.IsProviderConnected(OAuthBrokerProvider.Github));
    Assert.Equal(1, handler.RequestCount);
    Assert.Equal(3, session.RefreshCount);
  }

  private sealed class DisconnectHandler : HttpMessageHandler
  {
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      RequestCount++;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }
  }

  private sealed class FailingRefreshSessionStore :
      ISessionStore,
      ICommittedOAuthDisconnectSessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; private set; } = new(
        new User(
            "user-1",
            "testuser",
            GithubAccount: new UserDisplayAccount("github-1", "octocat")));

    public int RefreshCount { get; private set; }
    public bool FailRefresh { get; set; } = true;

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
      RefreshCount++;
      return FailRefresh
          ? Task.FromException(new InvalidOperationException("session refresh unavailable"))
          : Task.CompletedTask;
    }

    public void ApplyCommittedOAuthDisconnect(OAuthBrokerProvider provider)
    {
      var identity = Current.Identity ?? throw new InvalidOperationException("Identity is missing.");
      Current = new SessionSnapshot(identity with { GithubAccount = null });
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void RestoreConnectedIdentity() => Current = new SessionSnapshot(
        new User(
            "user-1",
            "testuser",
            GithubAccount: new UserDisplayAccount("github-1", "octocat")));
  }

  private sealed class UnusedBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Browser should not be called.");
  }

  private sealed class UnusedPersistence : INativeOAuthAuthorizationPersistence
  {
    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Persistence should not be called.");

    public Task WritePendingAsync(
        PendingNativeOAuthAuthorization pending,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Persistence should not be called.");

    public Task<bool> ClaimCallbackAsync(
        string flowId,
        string completionToken,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Persistence should not be called.");

    public Task<bool> CompleteAsync(
        string flowId,
        NativeOAuthAuthorizationResult result,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Persistence should not be called.");

    public Task ClearPendingAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Persistence should not be called.");

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Persistence should not be called.");
  }
}

using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationDisconnectConcurrencyTests
{
  [Fact]
  public async Task ConcurrentDisconnectsIssueOnlyOneRequest()
  {
    var handler = new BlockingDisconnectHandler();
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(handler)
        {
          BaseAddress = new Uri("https://api.test"),
        }),
        new UnusedPersistence(),
        new UnusedBrowser(),
        new SessionStore());

    var first = coordinator.DisconnectAsync(
        OAuthBrokerProvider.Github,
        TestContext.Current.CancellationToken);
    await handler.RequestStarted.WaitAsync(TestContext.Current.CancellationToken);
    var second = coordinator.DisconnectAsync(
        OAuthBrokerProvider.Github,
        TestContext.Current.CancellationToken);

    await second;
    Assert.Equal(1, handler.RequestCount);
    Assert.Equal(NativeOAuthAuthorizationState.Disconnecting, coordinator.State);

    handler.CompleteRequest();
    await first;

    Assert.Equal(1, handler.RequestCount);
    Assert.Equal(NativeOAuthAuthorizationState.Idle, coordinator.State);
  }

  private sealed class BlockingDisconnectHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource requestStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<HttpResponseMessage> response = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int requestCount;

    public Task RequestStarted => requestStarted.Task;
    public int RequestCount => requestCount;

    public void CompleteRequest() => response.SetResult(new HttpResponseMessage(HttpStatusCode.OK));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref requestCount);
      requestStarted.SetResult();
      return response.Task;
    }
  }

  private sealed class SessionStore : ISessionStore
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

  private sealed class UnusedPersistence : INativeOAuthAuthorizationPersistence
  {
    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

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
        throw new NotSupportedException();

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class UnusedBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}

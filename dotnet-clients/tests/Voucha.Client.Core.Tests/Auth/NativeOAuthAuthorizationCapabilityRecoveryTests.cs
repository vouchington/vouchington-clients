using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationCapabilityRecoveryTests
{
  [Fact]
  public async Task SuccessfulCapabilityReloadClearsCapabilityFailureWithoutActiveFlow()
  {
    var handler = new QueueHandler(
        new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
          Content = new StringContent("""{"message":"offline"}"""),
        },
        new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(ApiFixtureLoader.LoadResponse(
              "native.oauth.providers.broker-capabilities")),
        });
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        new EmptyPersistence(),
        new UnusedBrowser(),
        new AnonymousSessionStore());
    var retryStateChanges = 0;
    coordinator.PropertyChanged += (_, eventArgs) =>
    {
      if (eventArgs.PropertyName == nameof(coordinator.CanRetryCapabilityLoading))
      {
        retryStateChanges++;
      }
    };

    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);
    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.True(coordinator.CanRetryCapabilityLoading);

    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Idle, coordinator.State);
    Assert.Null(coordinator.ErrorMessage);
    Assert.False(coordinator.CanRetryCapabilityLoading);
    Assert.Equal(2, retryStateChanges);
    Assert.True(coordinator.Supports(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate));
  }

  [Fact]
  public async Task CapabilityReloadDoesNotClearUnrelatedPersistenceFailure()
  {
    var persistence = new EmptyPersistence
    {
      ReadException = new InvalidOperationException("secure storage unavailable"),
    };
    var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(ApiFixtureLoader.LoadResponse(
          "native.oauth.providers.broker-capabilities")),
    });
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        persistence,
        new UnusedBrowser(),
        new AnonymousSessionStore());
    await coordinator.ResumeAsync(TestContext.Current.CancellationToken);

    await coordinator.LoadCapabilitiesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Equal("secure storage unavailable", coordinator.ErrorMessage);
    Assert.False(coordinator.CanRetryCapabilityLoading);
  }

  private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
  {
    private readonly Queue<HttpResponseMessage> responses = new(responses);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(responses.Dequeue());
  }

  private sealed class EmptyPersistence : INativeOAuthAuthorizationPersistence
  {
    public Exception? ReadException { get; init; }

    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
        CancellationToken cancellationToken = default) =>
        ReadException is null
            ? Task.FromResult(new NativeOAuthAuthorizationSnapshot(null, null))
            : Task.FromException<NativeOAuthAuthorizationSnapshot>(ReadException);
    public Task WritePendingAsync(PendingNativeOAuthAuthorization pending, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<bool> ClaimCallbackAsync(string flowId, string completionToken, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<bool> CompleteAsync(string flowId, NativeOAuthAuthorizationResult result, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task ClearPendingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class UnusedBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Fediverse;

public sealed class NativeBlueskyLinkCoordinatorTests
{
  [Fact]
  public void CompletionProofIsHighEntropyAndS256Base64Url()
  {
    var proof = NativeBlueskyCompletionProof.Create();

    Assert.InRange(proof.Verifier.Length, 43, 128);
    Assert.Matches("^[A-Za-z0-9._~-]+$", proof.Verifier);
    Assert.Matches("^[A-Za-z0-9_-]{43}$", proof.Challenge);
    Assert.Equal(proof.Challenge, NativeBlueskyCompletionProof.FromVerifier(proof.Verifier).Challenge);
  }

  [Fact]
  public void CompletionProofsMatchRfc7636S256Vector()
  {
    const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    const string challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    Assert.Equal(challenge, NativeBlueskyCompletionProof.FromVerifier(verifier).Challenge);
    Assert.Equal(challenge, NativeAuthorizationCompletionProof.FromVerifier(verifier).Challenge);
  }

  [Fact]
  public async Task StartPersistsFlowAndOpensSystemBrowser()
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.auth.bluesky.link.native"));
    var persistence = new MemoryPersistence();
    var browser = new RecordingBrowser();
    var coordinator = Make(handler, persistence, browser);

    await coordinator.StartAsync("alice.bsky.social", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(NativeBlueskyLinkState.WaitingForCallback, coordinator.State);
    Assert.Equal("00000000-0000-7000-8000-00000000b501", persistence.Pending?.FlowId);
    Assert.Equal(new Uri("https://bsky.social/oauth/authorize"), browser.Opened);
  }

  [Fact]
  public async Task CallbackIsClaimedOnceThenFinalizedAndIdentityRefreshed()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.auth.bluesky.link-completion.default"), System.Net.HttpStatusCode.NoContent),
    ]);
    var persistence = new MemoryPersistence
    {
      Pending = new(
          "00000000-0000-7000-8000-00000000b501",
          "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          DateTimeOffset.UtcNow),
    };
    var session = new RecordingSessionStore
    {
      IdentitiesAfterRefresh = [LinkedIdentity()],
    };
    var coordinator = Make(handler, persistence, new RecordingBrowser(), session);
    var callback = new Uri("voucha://auth/bluesky/callback?flow_id=00000000-0000-7000-8000-00000000b501&completion_token=token");

    Assert.True(await coordinator.HandleCallbackAsync(callback, TestContext.Current.CancellationToken));
    Assert.False(await coordinator.HandleCallbackAsync(callback, TestContext.Current.CancellationToken));

    Assert.Equal(NativeBlueskyLinkState.Connected, coordinator.State);
    Assert.Equal(1, session.RefreshCount);
  }

  [Fact]
  public async Task ResumeExpiresOldFlow()
  {
    var persistence = new MemoryPersistence
    {
      Pending = new("flow", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow.AddMinutes(-11)),
    };
    var coordinator = Make(new RecordingHandler("null"), persistence, new RecordingBrowser());

    await coordinator.ResumeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

    Assert.Equal(NativeBlueskyLinkState.Expired, coordinator.State);
    Assert.Null(persistence.Pending);
  }

  [Fact]
  public async Task FailedCompletionRemainsRetryableAcrossResume()
  {
    var handler = new RecordingHandler([
      new RecordedResponse("{\"error\":\"temporary\"}", System.Net.HttpStatusCode.ServiceUnavailable),
      new RecordedResponse("", System.Net.HttpStatusCode.NoContent),
    ]);
    var persistence = new MemoryPersistence
    {
      Pending = new("flow", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow),
    };
    var session = new RecordingSessionStore
    {
      IdentitiesAfterRefresh = [null, LinkedIdentity()],
    };
    var coordinator = Make(handler, persistence, new RecordingBrowser(), session);
    var callback = new Uri("voucha://auth/bluesky/callback?flow_id=flow&completion_token=token");

    Assert.True(await coordinator.HandleCallbackAsync(callback, TestContext.Current.CancellationToken));
    Assert.Equal(NativeBlueskyLinkState.Failed, coordinator.State);
    Assert.Equal("token", persistence.Pending?.CompletionToken);

    await coordinator.ResumeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

    Assert.Equal(NativeBlueskyLinkState.Connected, coordinator.State);
    Assert.Null(persistence.Pending);
  }

  [Fact]
  public async Task SuccessfulCompletionWithStaleUnlinkedIdentityRemainsRetryable()
  {
    var persistence = new MemoryPersistence
    {
      Pending = new("flow", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow, "token"),
    };
    var session = new RecordingSessionStore();
    var coordinator = Make(
        new RecordingHandler("", System.Net.HttpStatusCode.NoContent),
        persistence,
        new RecordingBrowser(),
        session);

    await coordinator.ResumeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

    Assert.Equal(NativeBlueskyLinkState.Failed, coordinator.State);
    Assert.False(coordinator.IsLinked);
    Assert.Equal("token", persistence.Pending?.CompletionToken);
    Assert.Equal(1, session.RefreshCount);
  }

  [Fact]
  public async Task LostFinalizeResponseReconcilesLinkedIdentityAndClearsPendingCompletion()
  {
    var persistence = new MemoryPersistence
    {
      Pending = new("flow", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow, "token"),
    };
    var session = new RecordingSessionStore
    {
      IdentitiesAfterRefresh = [LinkedIdentity()],
    };
    var coordinator = Make(
        new RecordingHandler("{\"error\":\"response_lost\"}", System.Net.HttpStatusCode.ServiceUnavailable),
        persistence,
        new RecordingBrowser(),
        session);

    await coordinator.ResumeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

    Assert.Equal(NativeBlueskyLinkState.Connected, coordinator.State);
    Assert.True(coordinator.IsLinked);
    Assert.Null(persistence.Pending);
    Assert.Equal(1, session.RefreshCount);
  }

  [Fact]
  public async Task GenuineFinalizeFailureKeepsClaimedCompletionRetryable()
  {
    var persistence = new MemoryPersistence
    {
      Pending = new("flow", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow, "token"),
    };
    var session = new RecordingSessionStore();
    var coordinator = Make(
        new RecordingHandler("{\"error\":\"temporary\"}", System.Net.HttpStatusCode.ServiceUnavailable),
        persistence,
        new RecordingBrowser(),
        session);

    await coordinator.ResumeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

    Assert.Equal(NativeBlueskyLinkState.Failed, coordinator.State);
    Assert.Equal("token", persistence.Pending?.CompletionToken);
    Assert.Equal(1, session.RefreshCount);
  }

  [Fact]
  public async Task ProviderFailureIsClaimedAndCancelClearsPendingFlow()
  {
    var persistence = new MemoryPersistence
    {
      Pending = new("flow", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow),
    };
    var coordinator = Make(new RecordingHandler("null"), persistence, new RecordingBrowser());

    Assert.True(await coordinator.HandleCallbackAsync(
        new Uri("voucha://auth/bluesky/callback?flow_id=flow&bluesky_error=access_denied"),
        TestContext.Current.CancellationToken));
    Assert.Equal("access_denied", coordinator.ErrorCode);
    Assert.Equal(NativeBlueskyLinkState.Failed, coordinator.State);
    Assert.Null(persistence.Pending);

    persistence.Pending = new("other", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow);
    await coordinator.CancelAsync(TestContext.Current.CancellationToken);
    Assert.Equal(NativeBlueskyLinkState.Cancelled, coordinator.State);
    Assert.Null(persistence.Pending);
  }

  [Fact]
  public async Task BrowserFailureClearsPendingProofAndReportsFailure()
  {
    var persistence = new MemoryPersistence();
    var coordinator = Make(
        new RecordingHandler(ApiFixtureLoader.LoadResponse("native.auth.bluesky.link.native")),
        persistence,
        new RecordingBrowser(canOpen: false));

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        coordinator.StartAsync("alice.bsky.social", cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal(NativeBlueskyLinkState.Failed, coordinator.State);
    Assert.Null(persistence.Pending);
  }

  [Fact]
  public async Task DisconnectRefreshesIdentityAndReturnsToIdle()
  {
    var session = new RecordingSessionStore();
    var coordinator = Make(
        new RecordingHandler("", System.Net.HttpStatusCode.NoContent),
        new MemoryPersistence(),
        new RecordingBrowser(),
        session);

    await coordinator.DisconnectAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NativeBlueskyLinkState.Idle, coordinator.State);
    Assert.Equal(1, session.RefreshCount);
  }

  [Fact]
  public async Task DisconnectFailureIsVisibleAndDoesNotRefreshIdentity()
  {
    var session = new RecordingSessionStore();
    var coordinator = Make(
        new RecordingHandler("{\"error\":\"temporary\"}", System.Net.HttpStatusCode.ServiceUnavailable),
        new MemoryPersistence(),
        new RecordingBrowser(),
        session);

    await Assert.ThrowsAsync<VouchaApiException>(() =>
        coordinator.DisconnectAsync(TestContext.Current.CancellationToken));

    Assert.Equal(NativeBlueskyLinkState.Failed, coordinator.State);
    Assert.Equal(0, session.RefreshCount);
  }

  private static NativeBlueskyLinkCoordinator Make(
      RecordingHandler handler,
      MemoryPersistence persistence,
      RecordingBrowser browser,
      ISessionStore? session = null) =>
      new(
          new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
          persistence,
          browser,
          session ?? new RecordingSessionStore());

  private static User LinkedIdentity() =>
      new("user-1", "alice", BlueskyAccount: new("did:plc:alice", "alice.bsky.social"));

  private sealed class MemoryPersistence : INativeBlueskyLinkPersistence
  {
    public PendingNativeBlueskyLink? Pending { get; set; }
    public Task<PendingNativeBlueskyLink?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Pending);
    public Task WriteAsync(PendingNativeBlueskyLink pending, CancellationToken cancellationToken = default)
    {
      Pending = pending;
      return Task.CompletedTask;
    }
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
      Pending = null;
      return Task.CompletedTask;
    }
    public Task<bool> ClaimFailureAsync(string flowId, CancellationToken cancellationToken = default)
    {
      var claimed = Pending is { CompletionToken: null } && Pending.FlowId == flowId;
      if (claimed) Pending = null;
      return Task.FromResult(claimed);
    }
    public Task<bool> ClaimCallbackAsync(
        string flowId,
        string completionToken,
        CancellationToken cancellationToken = default)
    {
      var claimed = Pending is { CompletionToken: null } && Pending.FlowId == flowId;
      if (claimed) Pending = Pending! with { CompletionToken = completionToken };
      return Task.FromResult(claimed);
    }
  }

  private sealed class RecordingBrowser(bool canOpen = true) : INativeExternalBrowser
  {
    public Uri? Opened { get; private set; }
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
      Opened = uri;
      return Task.FromResult(canOpen);
    }
  }

  private sealed class RecordingSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = SessionSnapshot.Anonymous;
    public IReadOnlyList<User?> IdentitiesAfterRefresh { get; init; } = [];
    public int RefreshCount { get; private set; }
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
      RefreshCount++;
      var index = Math.Min(RefreshCount, IdentitiesAfterRefresh.Count) - 1;
      Current = new SessionSnapshot(index >= 0 ? IdentitiesAfterRefresh[index] : null);
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
      return Task.CompletedTask;
    }
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

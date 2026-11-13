using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Fact]
  public async Task BlueskyDisconnectRefreshesAfterMutationWithoutRepeatingCompletedRotationOrPublishingOldIdentity()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var linked = true;
    var identities = 0;
    var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      if (request.Method == HttpMethod.Delete)
      {
        linked = false;
        return Json(HttpStatusCode.OK, new { });
      }
      var wasLinked = linked;
      if (Interlocked.Increment(ref identities) == 2)
      {
        held.TrySetResult();
        await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      }
      return Json(HttpStatusCode.OK, new
      {
        identity = new { id = "user-1", username = "alice", bluesky_account = wasLinked ? new { did = "did:plc:alice", handle = "alice.bsky.social" } : null },
      });
    };
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    var coordinator = new NativeBlueskyLinkCoordinator(fixture.Client, new UnusedRound2BlueskyPersistence(),
        new UnusedRound2Browser(), fixture.Store);
    Assert.True(coordinator.IsLinked);
    var old = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Task? disconnect = null;
    try
    {
      await Task.WhenAny(held.Task, old).WaitAsync(TestContext.Current.CancellationToken);
      if (old.IsCompleted) await old;
      await held.Task.WaitAsync(TestContext.Current.CancellationToken);
      var rotations = fixture.Terminal.RenewCount;
      disconnect = coordinator.DisconnectAsync(TestContext.Current.CancellationToken);
      await Task.WhenAny(disconnect, held.Task).WaitAsync(TestContext.Current.CancellationToken);
      // Let the old GET finish only after the mutation and forced-refresh call have run.
      release.TrySetResult();
      await disconnect;
      Assert.False(coordinator.IsLinked);
      Assert.Null(coordinator.LinkedHandle);
      Assert.Equal(NativeBlueskyLinkState.Idle, coordinator.State);
      Assert.Equal(rotations, fixture.Terminal.RenewCount);
    }
    finally { release.TrySetResult(); }
    await old;
    Assert.False(coordinator.IsLinked);
    Assert.Null(fixture.Store.Current.Identity!.BlueskyAccount);
    Assert.Equal(3, identities);
  }

  [Fact]
  public async Task ForcedRefreshWhileRotationPendingSharesRotationAndLaterIdentityRequest()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var identities = 0;
    fixture.Terminal.Renew = async _ =>
    {
      entered.TrySetResult();
      await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    fixture.Terminal.Other = _ =>
    {
      identities++;
      return Task.FromResult(Json(HttpStatusCode.OK, new { identity = new { id = "user-1" } }));
    };
    var ordinary = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Task? forced = null;
    try
    {
      await Task.WhenAny(entered.Task, ordinary).WaitAsync(TestContext.Current.CancellationToken);
      if (ordinary.IsCompleted) await ordinary;
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      forced = ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
      Assert.False(forced.IsCompleted);
    }
    finally { release.TrySetResult(); }
    await Task.WhenAll(ordinary, forced!);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal(1, identities);
    Assert.True(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task PendingOAuthDispatchFencesOlderIdentityWithoutPublishingAnonymous()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var identities = 0;
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath.Contains("/complete", StringComparison.Ordinal))
        return Json(HttpStatusCode.Accepted, new { status = "pending" });
      var stale = ++identities > 1;
      if (stale)
      {
        entered.TrySetResult();
        await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      }
      return Json(HttpStatusCode.OK, new { identity = new { id = "user-1", username = stale ? "stale" : "retained" } });
    };
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    var snapshots = new List<SessionSnapshot>();
    fixture.Store.SessionChanged += (_, args) => snapshots.Add(args.Snapshot);
    var older = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    try
    {
      await Task.WhenAny(entered.Task, older).WaitAsync(TestContext.Current.CancellationToken);
      if (older.IsCompleted) await older;
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      var completion = await fixture.Client.CompleteOAuthAuthorizationAsync("flow", "completion", "proof",
          TestContext.Current.CancellationToken);
      Assert.Equal(OAuthCompletionKind.Pending, completion.Kind);
    }
    finally { release.TrySetResult(); }
    await older;
    Assert.Equal("retained", fixture.Store.Current.Identity?.Username);
    Assert.Empty(snapshots);
  }

  private sealed class UnusedRound2Browser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
  private sealed class UnusedRound2BlueskyPersistence : INativeBlueskyLinkPersistence
  {
    public Task<PendingNativeBlueskyLink?> ReadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task WriteAsync(PendingNativeBlueskyLink pending, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ClaimCallbackAsync(string flowId, string completionToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ClaimFailureAsync(string flowId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ClearAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}

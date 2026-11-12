using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData("terminal")]
  [InlineData("malformed")]
  [InlineData("anonymous")]
  [InlineData("persist")]
  public async Task SelfRenewalCleanupDiscardsCredentialsBeforeReleasingHeldPatchWaiters(string failure)
  {
    GatedPersistence? persistence = null;
    using var fixture = new RecoveryFixture(wrap: inner => persistence = new(inner));
    await fixture.SeedAsync();
    var entered = Gate();
    var release = Gate();
    fixture.Terminal.Renew = async _ =>
    {
      entered.TrySetResult();
      await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      return failure switch
      {
        "terminal" => Json(HttpStatusCode.Unauthorized, new { code = "SESSION_REFRESH_REUSED" }),
        "malformed" => Json(HttpStatusCode.OK, new { }),
        "anonymous" => Rotated(uid: null),
        _ => Rotated(),
      };
    };
    var invalidations = 0;
    fixture.Jar.SessionInvalidated += (_, _) => invalidations++;
    var renewal = BoundaryRotation(fixture);
    Task? waiter = null;
    try
    {
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      waiter = fixture.Jar.WaitForRenewalAsync(TestContext.Current.CancellationToken);
      Assert.False(waiter.IsCompleted);
      persistence!.Fail = failure == "persist";
    }
    finally { release.TrySetResult(); }
    var exception = await Assert.ThrowsAnyAsync<Exception>(() => renewal);
    Assert.Same(exception, await Assert.ThrowsAnyAsync<Exception>(() => waiter!));
    if (failure == "terminal") Assert.Equal("SESSION_REFRESH_REUSED", Assert.IsType<VouchaApiException>(exception).ErrorCode);
    else if (failure == "persist") Assert.IsType<IOException>(exception);
    else Assert.IsType<UnauthorizedAccessException>(exception);
    Assert.Equal(1, invalidations);
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Null(fixture.Persistence.Value);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

  [Fact]
  public async Task RejectedAutomaticClearAdmissionPreservesTheCurrentHeldPatch()
  {
    GatedPersistence? persistence = null;
    using var fixture = new RecoveryFixture(wrap: inner => persistence = new(inner));
    await fixture.SeedAsync();
    var responseEntered = Gate();
    var responseRelease = Gate();
    var renewalEntered = Gate();
    var renewalRelease = Gate();
    fixture.Terminal.Other = async _ =>
    {
      responseEntered.TrySetResult();
      await responseRelease.Task.WaitAsync(TestContext.Current.CancellationToken);
      return WithCookies("old-dt", "old-st");
    };
    fixture.Terminal.Renew = async _ =>
    {
      renewalEntered.TrySetResult();
      await renewalRelease.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    var attributes = fixture.RawAsync("/attributes");
    await responseEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    var renewal = BoundaryRotation(fixture);
    try
    {
      await renewalEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      persistence!.Block = true;
      responseRelease.TrySetResult();
      await persistence.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      var clear = fixture.Jar.ClearIfCurrentAsync(fixture.Jar.Generation, fixture.Jar.CredentialRevision);
      Assert.False(clear.IsCompleted);
      fixture.Jar.BeginAuthentication();
      persistence.Release.TrySetResult();
      await clear;
      Assert.Equal("old-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    }
    finally { responseRelease.TrySetResult(); persistence!.Release.TrySetResult(); renewalRelease.TrySetResult(); }
    using var response = await attributes;
    await renewal;
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", fixture.Persistence.Value!);
  }

  [Fact]
  public async Task TerminalReplayClearAbandonsOnlyAfterDiscardingTheHeldLifecyclePatchCredentials()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var replayEntered = Gate();
    var replayRelease = Gate();
    var lifecycleEntered = Gate();
    var lifecycleRelease = Gate();
    fixture.Terminal.Other = async request =>
    {
      if (request.Headers.GetValues("Cookie").Single().Contains("st=old-st", StringComparison.Ordinal))
        return Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" });
      replayEntered.TrySetResult();
      await replayRelease.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Json(HttpStatusCode.Unauthorized, new { code = "SESSION_REFRESH_REUSED" });
    };
    fixture.Terminal.Renew = async _ =>
    {
      if (fixture.Terminal.RenewCount == 1) return Rotated();
      lifecycleEntered.TrySetResult();
      await lifecycleRelease.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated(dt: "late-dt", st: "late-st");
    };
    var work = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken);
    Task? lifecycle = null;
    try
    {
      await replayEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      lifecycle = BoundaryRotation(fixture);
      await lifecycleEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      replayRelease.TrySetResult();
      var rejected = await Assert.ThrowsAsync<VouchaApiException>(() => work);
      Assert.Equal("SESSION_REFRESH_REUSED", rejected.ErrorCode);
      Assert.False(fixture.Jar.HasDeviceCookie);
      Assert.Null(fixture.Persistence.Value);
    }
    finally { replayRelease.TrySetResult(); lifecycleRelease.TrySetResult(); }
    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => lifecycle!);
    Assert.Null(fixture.Persistence.Value);
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Equal(2, fixture.Terminal.RenewCount);
  }

  [Theory]
  [InlineData("malformed-cookie-snapshot")]
  [InlineData("{\"Cookies\":[{\"Name\":\"st\",\"Value\":\"session-without-device\"}]}")]
  public async Task InvalidRestoreCompletesItsDiscardBeforeAnyBootstrapPatchCanStart(string snapshot)
  {
    var inner = new TestSessionCookiePersistence();
    await inner.WriteAsync(snapshot, TestContext.Current.CancellationToken);
    GatedRestorePersistence? persistence = null;
    using var fixture = new RecoveryFixture(inner, value => persistence = new(value));
    var work = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken);
    try
    {
      await persistence!.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.False(work.IsCompleted);
      Assert.Equal(0, fixture.Terminal.RenewCount);
    }
    finally { persistence!.Release.TrySetResult(); }
    await work;
    Assert.Equal(1, inner.ClearCount);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", inner.Value!);
  }

  [Fact]
  public async Task PersistingAnEmptySnapshotCommitsItsDiscardBeforeAbandoningHeldBootstrap()
  {
    using var fixture = new RecoveryFixture();
    var entered = Gate();
    var release = Gate();
    fixture.Terminal.Renew = async _ =>
    {
      entered.TrySetResult();
      await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    var bootstrap = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken);
    try
    {
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      await fixture.Jar.PersistAsync(TestContext.Current.CancellationToken);
      Assert.False(fixture.Jar.HasDeviceCookie);
      Assert.Null(fixture.Persistence.Value);
      Assert.Equal(1, fixture.Persistence.ClearCount);
    }
    finally { release.TrySetResult(); }
    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bootstrap);
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Null(fixture.Persistence.Value);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

  private sealed class GatedRestorePersistence(TestSessionCookiePersistence inner) : ISessionCookiePersistence
  {
    public TaskCompletionSource Entered { get; } = Gate();
    public TaskCompletionSource Release { get; } = Gate();
    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
      Entered.TrySetResult();
      await Release.Task.WaitAsync(cancellationToken);
      return await inner.ReadAsync(cancellationToken);
    }
    public Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default) => inner.WriteAsync(serializedCookies, cancellationToken);
    public Task ClearAsync(CancellationToken cancellationToken = default) => inner.ClearAsync(cancellationToken);
  }
}

using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData("expired", false)]
  [InlineData("expired", true)]
  [InlineData("malformed", false)]
  [InlineData("malformed", true)]
  public async Task ValidLoginCanReplaceAnUnusableRenewalInBothCompletionOrders(string failure, bool renewalFirst)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var renewing = Gate();
    var authenticating = Gate();
    var releaseRenewal = Gate();
    var releaseAuthentication = Gate();
    fixture.Terminal.Renew = async _ =>
    {
      renewing.TrySetResult();
      await releaseRenewal.Task.WaitAsync(TestContext.Current.CancellationToken);
      return UnusableRotation(failure);
    };
    fixture.Terminal.Other = async request =>
    {
      Assert.Equal("/api/v1/auth/email-address/login", request.RequestUri!.AbsolutePath);
      Assert.Contains("st=old-st", request.Headers.GetValues("Cookie").Single());
      authenticating.TrySetResult();
      await releaseAuthentication.Task.WaitAsync(TestContext.Current.CancellationToken);
      return WithCookies("valid-dt", "valid-st");
    };
    var refresh = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Task? authentication = null;
    try
    {
      await renewing.Task.WaitAsync(TestContext.Current.CancellationToken);
      authentication = fixture.Client.SendAsync(AuthenticationRequest("failed"), TestContext.Current.CancellationToken);
      await authenticating.Task.WaitAsync(TestContext.Current.CancellationToken);
      if (renewalFirst)
      {
        releaseRenewal.TrySetResult();
        await refresh;
        Assert.False(fixture.Jar.HasDeviceCookie);
        Assert.Null(fixture.Persistence.Value);
        releaseAuthentication.TrySetResult();
      }
      else
      {
        releaseAuthentication.TrySetResult();
        await authentication;
        releaseRenewal.TrySetResult();
      }
    }
    finally { releaseRenewal.TrySetResult(); releaseAuthentication.TrySetResult(); }
    await Task.WhenAll(refresh, authentication!);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal("valid-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("valid-st", fixture.Persistence.Value!);
    Assert.DoesNotContain("old-st", fixture.Persistence.Value!);
  }

  [Theory]
  [InlineData("logout")]
  [InlineData("mfa")]
  [InlineData("pending")]
  public async Task ImplicitRenewalClearDoesNotLetLoginOverrideLaterUserIntent(string newerIntent)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var renewing = Gate();
    var authenticating = Gate();
    var releaseRenewal = Gate();
    var releaseAuthentication = Gate();
    var rotations = 0;
    fixture.Terminal.Renew = async _ =>
    {
      if (Interlocked.Increment(ref rotations) > 1)
        return Rotated(uid: null, dt: "bootstrap-dt", st: "bootstrap-st");
      renewing.TrySetResult();
      await releaseRenewal.Task.WaitAsync(TestContext.Current.CancellationToken);
      return UnusableRotation("expired");
    };
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/auth/email-address/login")
      {
        authenticating.TrySetResult();
        await releaseAuthentication.Task.WaitAsync(TestContext.Current.CancellationToken);
        return WithCookies("obsolete-dt", "obsolete-st");
      }
      return UnchangedAuthentication(newerIntent);
    };
    var refresh = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Task? authentication = null;
    try
    {
      await renewing.Task.WaitAsync(TestContext.Current.CancellationToken);
      authentication = fixture.Client.SendAsync(AuthenticationRequest("failed"), TestContext.Current.CancellationToken);
      await authenticating.Task.WaitAsync(TestContext.Current.CancellationToken);
      releaseRenewal.TrySetResult();
      await refresh;
      Assert.False(fixture.Jar.HasDeviceCookie);
      if (newerIntent == "logout") await fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
      else await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, newerIntent == "mfa"
          ? "/api/v1/auth/mfa/totp/verification" : "/api/v1/auth/oauth/authorizations/flow/complete"), TestContext.Current.CancellationToken);
    }
    finally { releaseRenewal.TrySetResult(); releaseAuthentication.TrySetResult(); }
    await authentication!;
    if (newerIntent == "logout") Assert.Null(fixture.Persistence.Value);
    else
    {
      Assert.Equal("bootstrap-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
      Assert.DoesNotContain("obsolete-st", fixture.Persistence.Value!);
    }
    Assert.False(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task LogoutCancelsAuthenticationQueuedForRequestPreparationBeforeItReachesTransport()
  {
    GatedRenewalPersistence? persistence = null;
    using var fixture = new RecoveryFixture(wrap: inner => persistence = new(inner));
    await fixture.SeedAsync();
    var authRequests = 0;
    fixture.Terminal.Other = request =>
    {
      if (request.RequestUri!.AbsolutePath != "/api/v1/auth/logout") Interlocked.Increment(ref authRequests);
      return Task.FromResult(Json(HttpStatusCode.OK, new { }));
    };
    persistence!.Hold = true;
    var refresh = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Task? authentication = null;
    Task? logout = null;
    try
    {
      await persistence.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      authentication = fixture.Client.SendAsync(AuthenticationRequest("failed"), TestContext.Current.CancellationToken);
      Assert.False(authentication.IsCompleted);
      logout = fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
    }
    finally { persistence.Release.TrySetResult(); }
    await Task.WhenAll(refresh, logout!);
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authentication!);
    Assert.Equal(0, authRequests);
    Assert.Null(fixture.Persistence.Value);
  }

  private static HttpResponseMessage UnusableRotation(string failure) => failure == "expired"
      ? Json(HttpStatusCode.Unauthorized, new { code = "SESSION_REFRESH_REUSED" })
      : Json(HttpStatusCode.OK, new { });

  private sealed class GatedRenewalPersistence(TestSessionCookiePersistence inner) : ISessionCookiePersistence
  {
    public bool Hold { get; set; }
    public TaskCompletionSource Entered { get; } = Gate();
    public TaskCompletionSource Release { get; } = Gate();
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => inner.ReadAsync(cancellationToken);
    public Task ClearAsync(CancellationToken cancellationToken = default) => inner.ClearAsync(cancellationToken);
    public async Task WriteAsync(string value, CancellationToken cancellationToken = default)
    {
      if (Hold)
      {
        Entered.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken);
      }
      await inner.WriteAsync(value, cancellationToken);
    }
  }
}

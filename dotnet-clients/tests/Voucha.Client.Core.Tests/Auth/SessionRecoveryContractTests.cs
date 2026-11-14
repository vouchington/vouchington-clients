using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task ServerSecureFlagAndCanonicalCookieAttributesSurviveRestart(bool secure)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Renew = _ => Task.FromResult(Rotated(secure: secure));
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    var restoredContainer = new CookieContainer();
    using var restored = new SessionCookieJar(restoredContainer, Origin, fixture.Persistence);
    await restored.RestoreAsync(TestContext.Current.CancellationToken);
    foreach (var name in new[] { "dt", "st" })
    {
      var cookie = restoredContainer.GetCookies(Origin)[name]!;
      Assert.Equal(secure, cookie.Secure);
      Assert.True(cookie.HttpOnly);
      Assert.Equal("/", cookie.Path);
      Assert.Equal(Origin.Host, cookie.Domain);
    }
    Assert.Equal(!secure, restoredContainer.GetCookieHeader(new Uri("http://voucha.test")).Contains("st=new-st", StringComparison.Ordinal));
    Assert.Empty(restoredContainer.GetCookieHeader(new Uri("https://sub.voucha.test")));
    Assert.True(restored.CanRecoverAuthentication);
  }

  [Fact]
  public async Task ServerErrorWithTerminalCodePreservesAuthenticatedSnapshotAndCookies()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    var before = fixture.Persistence.Value;
    fixture.Terminal.Renew = _ => Task.FromResult(Json(HttpStatusCode.InternalServerError,
        new { code = "SESSION_RELOGIN_REQUIRED" }));
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(fixture.Store.Current.IsAuthenticated);
    Assert.Equal(before, fixture.Persistence.Value);
    await Assert.ThrowsAsync<VouchaApiException>(() => ((IConfirmingSessionRefreshStore)fixture.Store)
        .RefreshConfirmingAsync(TestContext.Current.CancellationToken));
    Assert.True(fixture.Store.Current.IsAuthenticated);
    Assert.Equal(before, fixture.Persistence.Value);
  }

  [Theory]
  [InlineData("/api/v1/auth/mfa/re-auth/email/tokens")]
  [InlineData("/api/v1/auth/mfa/re-auth/totp/verification")]
  [InlineData("/api/v1/auth/re-auth/passkeys/options")]
  [InlineData("/api/v1/auth/oauth/google/connect")]
  public async Task AccountConfirmationAndProviderChangesKeepCurrentIdentity(string path)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    fixture.Terminal.Other = _ => Task.FromResult(Json(HttpStatusCode.OK, new { }));
    await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, path), TestContext.Current.CancellationToken);
    Assert.True(fixture.Store.Current.IsAuthenticated);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

  [Theory]
  [InlineData("/api/v1/auth/email-address/login")]
  [InlineData("/api/v1/auth/native/email-address/login")]
  [InlineData("/api/v1/auth/mfa/totp/verification")]
  [InlineData("/api/v1/auth/passkeys/authentication/verify")]
  [InlineData("/api/v1/auth/oauth/apple/continue")]
  [InlineData("/api/v1/auth/oauth/authorizations/flow/complete")]
  public async Task LoginTransitionsFenceDispatchWithoutPublishingPrematureSignOut(string path)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    fixture.Terminal.Other = _ =>
    {
      Assert.Equal("user-1", fixture.Store.Current.Identity?.Id);
      return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    };
    await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Post, path), TestContext.Current.CancellationToken));
    Assert.Equal("user-1", fixture.Store.Current.Identity?.Id);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

  [Fact]
  public async Task ReplayedUnauthorizedCannotClearAccountCommittedWhileReplayWasInFlight()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath.StartsWith("/api/v1/auth/", StringComparison.Ordinal))
        return WithCookies("account-dt", "account-st");
      if (request.Headers.GetValues("Cookie").Single().Contains("st=new-st", StringComparison.Ordinal))
      {
        entered.SetResult();
        await release.Task;
      }
      return Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" });
    };
    var stale = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"),
        TestContext.Current.CancellationToken);
    await entered.Task;
    await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login"),
        TestContext.Current.CancellationToken);
    release.SetResult();
    await Assert.ThrowsAsync<VouchaApiException>(() => stale);
    Assert.Equal("account-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("account-st", fixture.Persistence.Value!);
  }

  [Fact]
  public async Task KnownAnonymousBootstrapRemainsNonRecoverableAfterRestart()
  {
    using var fixture = new RecoveryFixture();
    fixture.Terminal.Renew = _ => Task.FromResult(Rotated(uid: null, dt: "anonymous-dt", st: "anonymous-st"));
    fixture.Terminal.Other = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    using var restored = new SessionCookieJar(new CookieContainer(), Origin, fixture.Persistence);
    await restored.RestoreAsync(TestContext.Current.CancellationToken);
    Assert.False(restored.CanRecoverAuthentication);
    fixture.Terminal.Other = _ => Task.FromResult(Json(HttpStatusCode.OK, new { identity = (object?)null }));
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
    Assert.False(fixture.Store.Current.IsAuthenticated);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }
  [Fact]
  public async Task DirectSessionPatchPreservesExplicitBodyAndFencedResponseCookies()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Renew = async request =>
    {
      var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
      Assert.Contains("explicit-device", body);
      Assert.Contains("explicit-session", body);
      var response = Rotated(dt: "manual-dt", st: "manual-st");
      response.Headers.Add("Set-Cookie", "dt=manual-dt; Path=/; HttpOnly; Secure");
      response.Headers.Add("Set-Cookie", "st=manual-st; Path=/; HttpOnly; Secure");
      return response;
    };
    var result = await fixture.Client.SendAsync<NativeSessionRefreshResponse>(
        VouchaApiEndpoints.RefreshSession("explicit-device", "explicit-session"), TestContext.Current.CancellationToken);
    Assert.Equal("manual-st", result.Session.St);
    Assert.Equal("manual-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("manual-st", fixture.Persistence.Value!);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task EarlierResponseCredentialCommitFencesInFlightRenewalResultAndFailure(bool failure)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var responseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var responseRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var renewEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var renewRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async _ =>
    {
      responseEntered.SetResult();
      await responseRelease.Task;
      return WithCookies("manual-dt", "manual-st");
    };
    var older = fixture.RawAsync("/credential-update");
    await responseEntered.Task;
    var renewal = fixture.Jar.RenewAsync(async (_, _, _) =>
    {
      renewEntered.SetResult();
      await renewRelease.Task;
      if (failure) throw new VouchaApiException(HttpStatusCode.Unauthorized, "{\"code\":\"AUTH_REQUIRED\"}");
      return new NativeSessionRefreshResponse(new NativeRefreshedSession("device", "renewal-dt", 120, true,
          new NativeRefreshedSessionClaims("web", "device", 1, [], 1, "session", "user-1"),
          "session", "renewal-st", 60, "user-1"));
    }, cancellationToken: TestContext.Current.CancellationToken);
    await renewEntered.Task;
    responseRelease.SetResult();
    using var response = await older;
    renewRelease.SetResult();
    if (failure) await Assert.ThrowsAsync<VouchaApiException>(() => renewal);
    else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => renewal);
    Assert.Equal("manual-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("manual-st", fixture.Persistence.Value!);
  }
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SuppliedUserAgentIsForwardedUnchangedThroughBootstrapRenewalAndReplay(bool seeded)
  {
    using var fixture = new RecoveryFixture();
    if (seeded) await fixture.SeedAsync();
    fixture.SetUserAgent("ExistingStack/7.2 (custom)");
    fixture.Terminal.Renew = request =>
    {
      Assert.Equal("ExistingStack/7.2 (custom)", string.Join(" ", request.Headers.GetValues("User-Agent")));
      return Task.FromResult(Rotated(uid: seeded ? "user-1" : null));
    };
    var observed = 0;
    fixture.Terminal.Other = request =>
    {
      Assert.Equal("ExistingStack/7.2 (custom)", string.Join(" ", request.Headers.GetValues("User-Agent")));
      observed++;
      var fresh = request.Headers.GetValues("Cookie").Single().Contains("st=new-st", StringComparison.Ordinal);
      return Task.FromResult(fresh ? Json(HttpStatusCode.OK, new { }) :
          Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    };
    await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken);
    Assert.Equal(seeded ? 2 : 1, observed);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

  [Fact]
  public async Task AppAndSdkMetadataUpdatesDoNotInvalidateSessionOrIdentity()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    var epoch = fixture.Jar.Generation;
    var revision = fixture.Jar.CredentialRevision;
    var persisted = fixture.Persistence.Value;
    fixture.Terminal.Other = request =>
    {
      Assert.Equal("9.0", request.Headers.GetValues("x-voucha-app-version").Single());
      Assert.Equal("8.0", request.Headers.GetValues("x-voucha-sdk-version").Single());
      return Task.FromResult(Json(HttpStatusCode.OK, new { }));
    };
    using var updated = new HttpClient(new ClientMetadataHandler(new(ClientPlatform.Windows, "9.0", "8.0"),
        Origin, fixture.Jar, new SessionCookiePersistenceHandler(fixture.Jar, fixture.Terminal)))
    { BaseAddress = Origin };
    using var response = await updated.GetAsync("/api/v1/posts", TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(fixture.Store.Current.IsAuthenticated);
    Assert.Equal(epoch, fixture.Jar.Generation);
    Assert.Equal(revision, fixture.Jar.CredentialRevision);
    Assert.Equal(persisted, fixture.Persistence.Value);
  }
}

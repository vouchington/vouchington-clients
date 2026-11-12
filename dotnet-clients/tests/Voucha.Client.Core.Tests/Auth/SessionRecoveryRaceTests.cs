using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Fact]
  public async Task RotationPersistenceBlocksReplayAndNewRequestsUntilDurable()
  {
    GatedPersistence? gated = null;
    using var fixture = new RecoveryFixture(wrap: inner => gated = new(inner));
    await fixture.SeedAsync();
    gated!.Block = true;
    var first = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"),
        TestContext.Current.CancellationToken);
    await gated.Entered.Task;
    var second = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/notifications"),
        TestContext.Current.CancellationToken);
    var forced = ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true,
        TestContext.Current.CancellationToken);
    Assert.False(first.IsCompleted);
    Assert.False(second.IsCompleted);
    Assert.False(forced.IsCompleted);
    Assert.Equal(0, fixture.Terminal.SuccessfulReplays);
    Assert.Contains("old-st", fixture.Persistence.Value!);
    gated.Release.SetResult();
    await Task.WhenAll(first, second, forced);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal(2, fixture.Terminal.SuccessfulReplays);
    Assert.True(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task FailedRotationPersistenceClearsUncommittedPairAndDoesNotReplay()
  {
    GatedPersistence? gated = null;
    using var fixture = new RecoveryFixture(wrap: inner => gated = new(inner));
    await fixture.SeedAsync();
    gated!.Fail = true;
    await Assert.ThrowsAsync<IOException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Null(fixture.Persistence.Value);
    Assert.Equal(0, fixture.Terminal.SuccessfulReplays);
    Assert.False(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task OldResponseCookiesCannotOverwriteCommittedRotation()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath != "/slow")
        return Json(HttpStatusCode.OK, new { identity = new { id = "user-1" } });
      started.SetResult();
      await release.Task;
      return WithCookies("old-dt", "old-st");
    };
    var oldRequest = fixture.RawAsync("/slow");
    await started.Task;
    await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
    release.SetResult();
    using var response = await oldRequest;
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Contains("new-st", fixture.Persistence.Value!);
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SessionChangeFencesDelayedRenewalBeforeAwaitingLogoutOrAccountSwitch(bool switchAccount)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Renew = async _ =>
    {
      started.SetResult();
      await release.Task;
      var response = Rotated();
      response.Headers.Add("Set-Cookie", "st=old-account; Path=/; Secure");
      return response;
    };
    fixture.Terminal.Other = _ => Task.FromResult(switchAccount
        ? WithCookies("account-dt", "account-st") : Json(HttpStatusCode.OK, new { }));
    var stale = ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true,
        TestContext.Current.CancellationToken);
    await started.Task;
    if (switchAccount)
      await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login"),
          TestContext.Current.CancellationToken);
    else
      await fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
    Assert.False(fixture.Store.Current.IsAuthenticated);
    release.SetResult();
    await stale;
    Assert.False(fixture.Store.Current.IsAuthenticated);
    if (switchAccount)
      Assert.Equal("account-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    else
      Assert.Null(fixture.Persistence.Value);
  }

  [Fact]
  public async Task LoginCommitFencesRefreshStartedWhileLoginWasInFlight()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var loginEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var loginRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var renewEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var renewRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async _ =>
    {
      loginEntered.SetResult();
      await loginRelease.Task;
      return WithCookies("account-dt", "account-st");
    };
    fixture.Terminal.Renew = async _ =>
    {
      renewEntered.SetResult();
      await renewRelease.Task;
      return Rotated();
    };
    var login = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login"),
        TestContext.Current.CancellationToken);
    await loginEntered.Task;
    var staleRefresh = ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true,
        TestContext.Current.CancellationToken);
    await renewEntered.Task;
    loginRelease.SetResult();
    await login;
    renewRelease.SetResult();
    await staleRefresh;
    Assert.Equal("account-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("account-st", fixture.Persistence.Value!);
    Assert.False(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task CookieTransportEnforcesManualCookiesThroughDelegatingHandlers()
  {
    using var fixture = new RecoveryFixture();
    using var sockets = new SocketsHttpHandler { UseCookies = true };
    using var wrapper = new Passthrough(sockets);
    using var handler = new SessionCookiePersistenceHandler(fixture.Jar, wrapper);
    Assert.False(sockets.UseCookies);
    Assert.False(sockets.AllowAutoRedirect);
    using var http = new HttpClientHandler { UseCookies = true };
    using var other = new SessionCookiePersistenceHandler(fixture.Jar, http);
    Assert.False(http.UseCookies);
    Assert.False(http.AllowAutoRedirect);
  }

  [Fact]
  public async Task LogoutCapturesCommittedPairAtomicallyWhileRotationPersistenceIsBlocked()
  {
    GatedPersistence? gated = null;
    using var fixture = new RecoveryFixture(wrap: inner => gated = new(inner));
    await fixture.SeedAsync();
    gated!.Block = true;
    var refresh = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    await gated.Entered.Task;
    var logoutRequests = 0;
    fixture.Terminal.Other = request =>
    {
      Assert.Equal("/api/v1/auth/logout", request.RequestUri!.AbsolutePath);
      Assert.Equal("dt=new-dt; st=new-st", request.Headers.GetValues("Cookie").Single());
      logoutRequests++;
      return Task.FromResult(Json(HttpStatusCode.OK, new { }));
    };
    var logout = fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
    Assert.False(logout.IsCompleted);
    Assert.Equal(0, logoutRequests);
    gated.Release.SetResult();
    await Task.WhenAll(refresh, logout);
    Assert.Equal(1, logoutRequests);
    Assert.Null(fixture.Persistence.Value);
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.False(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task NewAccountBeforeCriticalLogoutClearPreservesNewPairWithoutLogoutRequest()
  {
    GatedPersistence? gated = null;
    using var fixture = new RecoveryFixture(wrap: inner => gated = new(inner));
    await fixture.SeedAsync();
    gated!.Block = true;
    var pending = fixture.Jar.StoreSessionTokensAsync("pending-dt", "pending-st",
        TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    await gated.Entered.Task;
    var requests = 0;
    fixture.Terminal.Other = _ => { requests++; return Task.FromResult(Json(HttpStatusCode.OK, new { })); };
    var logout = fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
    var newer = fixture.Jar.StoreSessionTokensAsync("account-dt", "account-st",
        TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    gated.Release.SetResult();
    await Task.WhenAll(pending, logout, newer);
    Assert.Equal(0, requests);
    Assert.Equal("account-dt", fixture.Jar.CookieContainer.GetCookies(Origin)["dt"]!.Value);
    Assert.Equal("account-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("account-st", fixture.Persistence.Value!);
  }

  [Fact]
  public async Task NewAccountCanRefreshWhileEarlierLogoutResponseIsPending()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/auth/logout")
      {
        entered.SetResult();
        await release.Task;
        return WithCookies("late-old-dt", "late-old-st");
      }
      if (request.RequestUri.AbsolutePath == "/api/v1/my/identity")
        return Json(HttpStatusCode.OK, new { identity = new { id = "new-user", username = "new-account" } });
      return WithCookies("account-dt", "account-st");
    };
    var logout = fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
    await entered.Task;
    await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login"),
        TestContext.Current.CancellationToken);
    fixture.Terminal.Renew = _ => Task.FromResult(Rotated(uid: "new-user", dt: "new-account-dt", st: "new-account-st"));
    await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
    Assert.Equal("new-user", fixture.Store.Current.Identity!.Id);
    release.SetResult();
    await logout;
    Assert.Equal("new-user", fixture.Store.Current.Identity!.Id);
    Assert.Equal("new-account-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task LogoutInvalidationEventCannotStartForegroundRefreshBeforeEpochIsRecorded(bool listenerBeforeStore)
  {
    var persistence = new TestSessionCookiePersistence();
    using var jar = new SessionCookieJar(new CookieContainer(), Origin, persistence);
    await jar.StoreSessionTokensAsync("old-dt", "old-st", TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1),
        TestContext.Current.CancellationToken);
    var terminal = new RecoveryTerminal(persistence);
    using var http = new HttpClient(new ClientMetadataHandler(new(ClientPlatform.Windows, "test"), Origin, jar,
        new SessionCookiePersistenceHandler(jar, terminal)))
    { BaseAddress = Origin };
    var client = new VouchaApiClient(http);
    CookieSessionStore? store = null;
    Task? nestedRefresh = null;
    EventHandler listener = (_, _) => nestedRefresh = ((IForcedSessionRefreshStore)store!)
        .RefreshAsync(true, TestContext.Current.CancellationToken);
    if (listenerBeforeStore) jar.SessionInvalidated += listener;
    store = new CookieSessionStore(client, jar);
    if (!listenerBeforeStore) jar.SessionInvalidated += listener;
    terminal.Other = request =>
    {
      Assert.Equal("/api/v1/auth/logout", request.RequestUri!.AbsolutePath);
      return Task.FromResult(Json(HttpStatusCode.OK, new { }));
    };
    await store.SignOutAsync(TestContext.Current.CancellationToken);
    await nestedRefresh!;
    Assert.Equal(0, terminal.RenewCount);
    Assert.False(store.Current.IsAuthenticated);
    Assert.Null(persistence.Value);
  }

  [Fact]
  public async Task RenewalStartedByClearInvalidationCannotRestoreClearedCredentials()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Task? renewal = null;
    var called = false;
    fixture.Jar.SessionInvalidated += (_, _) => renewal = fixture.Jar.RenewAsync(async (_, _, _) =>
    {
      called = true;
      await release.Task;
      return new NativeSessionRefreshResponse(new NativeRefreshedSession("device", "late-dt", 120, true,
          new NativeRefreshedSessionClaims("web", "device", 1, [], 1, "session", "user-1"),
          "session", "late-st", 60, "user-1"));
    }, cancellationToken: TestContext.Current.CancellationToken);
    await fixture.Jar.ClearAsync(TestContext.Current.CancellationToken);
    release.SetResult();
    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => renewal!);
    Assert.False(called);
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Null(fixture.Persistence.Value);
  }

  private static HttpResponseMessage WithCookies(string dt, string st)
  {
    var response = Json(HttpStatusCode.OK, new { });
    response.Headers.Add("Set-Cookie", $"dt={dt}; Path=/; HttpOnly; Secure");
    response.Headers.Add("Set-Cookie", $"st={st}; Path=/; HttpOnly; Secure");
    return response;
  }

  private sealed class Passthrough(HttpMessageHandler inner) : DelegatingHandler(inner);

  private sealed class GatedPersistence(TestSessionCookiePersistence inner) : ISessionCookiePersistence
  {
    public bool Block { get; set; }
    public bool Fail { get; set; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => inner.ReadAsync(cancellationToken);
    public Task ClearAsync(CancellationToken cancellationToken = default) => inner.ClearAsync(cancellationToken);
    public async Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default)
    {
      if (Fail) throw new IOException("secure storage unavailable");
      if (Block) { Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); }
      await inner.WriteAsync(serializedCookies, cancellationToken);
    }
  }
}

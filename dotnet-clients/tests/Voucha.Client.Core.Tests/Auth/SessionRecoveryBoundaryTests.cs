using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MalformedOrEmptyRenewalClearsUnusableSession(bool empty)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Renew = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(empty ? "null" : "not-json"),
    });
    if (empty)
      await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Client.SendAsync(
          new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    else
      await Assert.ThrowsAsync<JsonException>(() => fixture.Client.SendAsync(
          new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.Null(fixture.Persistence.Value);
    Assert.False(fixture.Jar.HasDeviceCookie);
  }

  [Fact]
  public async Task RenewalWithoutDeviceCannotCallTransport()
  {
    using var fixture = new RecoveryFixture();
    var called = false;
    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Jar.RenewAsync((_, _, _) =>
    {
      called = true;
      return Task.FromException<NativeSessionRefreshResponse>(new InvalidOperationException());
    }, cancellationToken: TestContext.Current.CancellationToken));
    Assert.False(called);
    Assert.False(fixture.Jar.HasDeviceCookie);
  }

  [Fact]
  public async Task CookieHandlerWaitsForJarRotationBeforeSnapshottingCookies()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Renew = async _ => { entered.SetResult(); await release.Task; return Rotated(); };
    var rotation = ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true,
        TestContext.Current.CancellationToken);
    await entered.Task;
    using var direct = new HttpClient(new SessionCookiePersistenceHandler(fixture.Jar,
        new CookieCheckingTerminal()))
    { BaseAddress = Origin };
    var request = direct.GetAsync("/api/v1/posts", TestContext.Current.CancellationToken);
    Assert.False(request.IsCompleted);
    release.SetResult();
    using var response = await request;
    await rotation;
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task LogoutClearsLocallyBeforeNetworkAndForegroundCannotRepublish(bool canceled)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(fixture.Store.Current.IsAuthenticated);
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      Assert.Equal("/api/v1/auth/logout", request.RequestUri!.AbsolutePath);
      Assert.Contains("st=new-st", request.Headers.GetValues("Cookie").Single());
      entered.SetResult();
      await release.Task;
      if (canceled) throw new TaskCanceledException();
      return Json(HttpStatusCode.ServiceUnavailable, new { code = "UNAVAILABLE" });
    };
    var logout = fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
    await entered.Task;
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Null(fixture.Persistence.Value);
    await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
    Assert.False(fixture.Store.Current.IsAuthenticated);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    release.SetResult();
    await logout;
    Assert.False(fixture.Store.Current.IsAuthenticated);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task BodylessGetExportsReplayBeforeConsumingSuccessfulContent(bool download)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    if (download)
    {
      var path = await fixture.Client.DownloadToTemporaryFileAsync(
          new ApiRequest(HttpMethod.Get, "/api/v1/posts"), "export.json", TestContext.Current.CancellationToken);
      try { Assert.Contains("ok", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)); }
      finally { File.Delete(path); }
    }
    else
    {
      var body = await fixture.Client.SendTextAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"),
          TestContext.Current.CancellationToken);
      Assert.Contains("ok", body);
    }
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal(1, fixture.Terminal.SuccessfulReplays);
  }

  [Fact]
  public async Task ConfirmingWaiterJoiningOrdinaryRefreshStillReportsTransientFailure()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Renew = async _ =>
    {
      entered.SetResult();
      await release.Task;
      throw new HttpRequestException("offline");
    };
    var ordinary = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    await entered.Task;
    var confirming = ((IConfirmingSessionRefreshStore)fixture.Store).RefreshConfirmingAsync(
        TestContext.Current.CancellationToken);
    release.SetResult();
    await ordinary;
    await Assert.ThrowsAsync<HttpRequestException>(() => confirming);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Contains("old-st", fixture.Persistence.Value!);
  }

  [Fact]
  public async Task SharedTransportTimeoutPreservesCurrentCookies()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Renew = _ => Task.FromException<HttpResponseMessage>(new TaskCanceledException());
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.Contains("old-st", fixture.Persistence.Value!);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

#if DEBUG
  [Fact]
  public async Task DevelopmentInjectionInvalidatesAndRotatesBeforePublishingIdentity()
  {
    using var fixture = new RecoveryFixture();
    await fixture.Store.InjectDevelopmentCookiesAsync("debug-dt", "debug-st", TestContext.Current.CancellationToken);
    Assert.True(fixture.Store.Current.IsAuthenticated);
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", fixture.Persistence.Value!);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }
#endif

  [Fact]
  public async Task LogoutNetworkFailureKeepsLocalJarCleared()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Other = _ => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"));
    await fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
    Assert.Null(fixture.Persistence.Value);
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.False(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task CredentialSnapshotReplacementBeforeRenewalReadsTokensRemainsUsable()
  {
    GatedPersistence? gated = null;
    using var fixture = new RecoveryFixture(wrap: inner => gated = new(inner));
    await fixture.SeedAsync();
    gated!.Block = true;
    var replacement = fixture.Jar.StoreSessionTokensAsync("account-dt", "account-st",
        TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    await gated.Entered.Task;
    var called = false;
    var renewal = fixture.Jar.RenewAsync((_, _, _) =>
    {
      called = true;
      return Task.FromResult(new NativeSessionRefreshResponse(new NativeRefreshedSession("device", "final-dt", 120, true,
          new NativeRefreshedSessionClaims("web", "device", 0, [], 0, "session", "member"), "session", "final-st", 60, "member")));
    }, cancellationToken: TestContext.Current.CancellationToken);
    gated.Release.SetResult();
    await Task.WhenAll(replacement, renewal);
    Assert.True(called);
    Assert.Contains("final-st", fixture.Persistence.Value!);
  }

  private sealed class CookieCheckingTerminal : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Assert.Contains("st=new-st", request.Headers.GetValues("Cookie").Single());
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
  }
}

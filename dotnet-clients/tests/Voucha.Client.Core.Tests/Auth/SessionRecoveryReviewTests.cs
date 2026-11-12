using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData("not-json", false)]
  [InlineData("", false)]
  [InlineData("null", false)]
  [InlineData("not-json", true)]
  [InlineData("", true)]
  [InlineData("null", true)]
  public async Task LifecycleRenewalDecodeFailureClearsConsumedCredentialsAndIdentity(string body, bool confirming)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(fixture.Store.Current.IsAuthenticated);
    fixture.Terminal.Renew = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(body),
    });
    if (confirming)
      await Assert.ThrowsAnyAsync<HttpRequestException>(() => ((IConfirmingSessionRefreshStore)fixture.Store)
          .RefreshConfirmingAsync(TestContext.Current.CancellationToken));
    else
      await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
    Assert.False(fixture.Store.Current.IsAuthenticated);
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Null(fixture.Persistence.Value);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SessionCookieExpirationDuringUnauthorizedRequestStillRenewsByDevice(bool networkFailure)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var requests = 0;
    fixture.Terminal.Other = request =>
    {
      requests++;
      if (requests == 1)
      {
        Assert.Contains("st=old-st", request.Headers.GetValues("Cookie").Single());
        fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Expired = true;
        return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
      }
      var fresh = request.Headers.GetValues("Cookie").Single().Contains("st=new-st", StringComparison.Ordinal);
      return Task.FromResult(Json(fresh ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
          new { code = fresh ? "OK" : "AUTH_REQUIRED" }));
    };
    fixture.Terminal.Renew = async request =>
    {
      var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
      Assert.Equal("old-dt", body.RootElement.GetProperty("dt").GetString());
      Assert.False(body.RootElement.TryGetProperty("st", out _));
      if (networkFailure) throw new HttpRequestException("offline");
      return Rotated();
    };
    var call = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"),
        TestContext.Current.CancellationToken);
    if (networkFailure) await Assert.ThrowsAnyAsync<HttpRequestException>(() => call);
    else await call;
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.True(fixture.Jar.HasDeviceCookie);
    Assert.Contains(networkFailure ? "old-dt" : "new-st", fixture.Persistence.Value!);
    Assert.Equal(networkFailure ? 1 : 2, requests);
  }

  [Fact]
  public async Task LoginResponseAfterCompletedRenewalCommitsAndPublishesTheNewAccount()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/auth/native/email-address/login")
      {
        entered.SetResult();
        await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        return WithCookies("account-dt", "account-st");
      }
      var account = request.Headers.GetValues("Cookie").Single().Contains("account", StringComparison.Ordinal);
      return Json(HttpStatusCode.OK, new { identity = new { id = account ? "new-user" : "user-1" } });
    };
    fixture.Terminal.Renew = async request =>
    {
      var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
      return body.Contains("account", StringComparison.Ordinal)
          ? Rotated(uid: "new-user", dt: "account-final-dt", st: "account-final-st") : Rotated();
    };
    var service = new AuthenticationService(fixture.Client, fixture.Store, new UnusedReviewPasskeyProvider());
    var login = service.VerifyEmailOtpAsync("new@example.test", "ABCDEFGH23", TestContext.Current.CancellationToken);
    try
    {
      await Task.WhenAny(entered.Task, login).WaitAsync(TestContext.Current.CancellationToken);
      if (login.IsCompleted) await login;
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
      Assert.Equal("user-1", fixture.Store.Current.Identity!.Id);
    }
    finally
    {
      release.TrySetResult();
    }
    await login;
    Assert.Equal("new-user", fixture.Store.Current.Identity!.Id);
    Assert.Equal("account-final-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("account-final-st", fixture.Persistence.Value!);
  }

  private sealed class UnusedReviewPasskeyProvider : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(PasskeyAuthenticationOptions options,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}

using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData(true, true)]
  [InlineData(true, false)]
  [InlineData(false, false)]
  public async Task NonAuthoritativeAuthenticationHeadersCannotOverrideRenewedCredentials(bool failedResponse, bool sessionHeaders)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/my/identity")
        return Json(HttpStatusCode.OK, new { identity = new { id = "user-1" } });
      entered.TrySetResult();
      await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      var response = sessionHeaders ? WithCookies("rejected-dt", "rejected-st") : Json(HttpStatusCode.OK, new { });
      if (!sessionHeaders) response.Headers.Add("Set-Cookie", "locale=rejected; Path=/");
      if (failedResponse) response.StatusCode = HttpStatusCode.BadRequest;
      return response;
    };
    var login = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login"),
        TestContext.Current.CancellationToken);
    try
    {
      await Task.WhenAny(entered.Task, login).WaitAsync(TestContext.Current.CancellationToken);
      if (login.IsCompleted) await login;
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
    }
    finally { release.TrySetResult(); }
    if (failedResponse) await Assert.ThrowsAsync<VouchaApiException>(() => login);
    else await login;
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Null(fixture.Jar.CookieContainer.GetCookies(Origin)["locale"]);
    Assert.DoesNotContain("rejected", fixture.Persistence.Value!);
    Assert.True(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task NonTransitionAuthResponseCannotOverrideRenewalRevision()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/my/identity")
        return Json(HttpStatusCode.OK, new { identity = new { id = "user-1" } });
      entered.TrySetResult();
      await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      return WithCookies("reauth-dt", "reauth-st");
    };
    var confirmation = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/mfa/re-auth/email/tokens"),
        TestContext.Current.CancellationToken);
    try
    {
      await Task.WhenAny(entered.Task, confirmation).WaitAsync(TestContext.Current.CancellationToken);
      if (confirmation.IsCompleted) await confirmation;
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
    }
    finally { release.TrySetResult(); }
    await confirmation;
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.DoesNotContain("reauth-st", fixture.Persistence.Value!);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SuccessfulLoginResponseCannotOverrideNewerLoginOrLogout(bool logout)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async request =>
    {
      if (request.Headers.TryGetValues("x-review-login", out var values) && values.Single() == "first")
      {
        entered.TrySetResult();
        await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        return WithCookies("first-dt", "first-st");
      }
      return WithCookies("second-dt", "second-st");
    };
    var first = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login")
    {
      Headers = new Dictionary<string, string> { ["x-review-login"] = "first" },
    }, TestContext.Current.CancellationToken);
    try
    {
      await Task.WhenAny(entered.Task, first).WaitAsync(TestContext.Current.CancellationToken);
      if (first.IsCompleted) await first;
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      if (logout) await fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
      else await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login"),
          TestContext.Current.CancellationToken);
    }
    finally { release.TrySetResult(); }
    await first;
    if (logout)
    {
      Assert.False(fixture.Jar.HasDeviceCookie);
      Assert.Null(fixture.Persistence.Value);
    }
    else
    {
      Assert.Equal("second-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
      Assert.Contains("second-st", fixture.Persistence.Value!);
    }
    Assert.False(fixture.Store.Current.IsAuthenticated);
  }
}

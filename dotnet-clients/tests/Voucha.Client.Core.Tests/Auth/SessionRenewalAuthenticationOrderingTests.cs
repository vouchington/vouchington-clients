using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData("failed")]
  [InlineData("mfa")]
  [InlineData("pending")]
  public async Task AuthenticationWithoutReplacementWaitsForAndRetainsPendingRenewal(string outcome)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var renewing = Gate();
    var releaseRenewal = Gate();
    var authRequests = 0;
    fixture.Terminal.Renew = async _ =>
    {
      renewing.TrySetResult();
      await releaseRenewal.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    fixture.Terminal.Other = request =>
    {
      Interlocked.Increment(ref authRequests);
      Assert.Contains("st=new-st", request.Headers.GetValues("Cookie").Single());
      return Task.FromResult(UnchangedAuthentication(outcome));
    };
    var refresh = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Task? authentication = null;
    try
    {
      await renewing.Task.WaitAsync(TestContext.Current.CancellationToken);
      authentication = fixture.Client.SendAsync(AuthenticationRequest(outcome), TestContext.Current.CancellationToken);
      Assert.False(authentication.IsCompleted);
      Assert.Equal(0, authRequests);
    }
    finally { releaseRenewal.TrySetResult(); }
    await refresh;
    if (outcome == "failed") await Assert.ThrowsAsync<VouchaApiException>(() => authentication!);
    else await authentication!;
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal(1, authRequests);
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", fixture.Persistence.Value!);
    Assert.DoesNotContain("old-st", fixture.Persistence.Value!);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task AuthenticationReplacementWinsBothCompletionOrdersAgainstLaterRenewal(bool renewalFirst)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var authenticating = Gate();
    var renewing = Gate();
    var releaseAuthentication = Gate();
    var releaseRenewal = Gate();
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/my/identity")
        return Json(HttpStatusCode.OK, new { identity = new { id = "user-1" } });
      authenticating.TrySetResult();
      await releaseAuthentication.Task.WaitAsync(TestContext.Current.CancellationToken);
      return WithCookies("account-dt", "account-st");
    };
    fixture.Terminal.Renew = async _ =>
    {
      renewing.TrySetResult();
      await releaseRenewal.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    var authentication = fixture.Client.SendAsync(AuthenticationRequest("failed"), TestContext.Current.CancellationToken);
    Task? refresh = null;
    try
    {
      await authenticating.Task.WaitAsync(TestContext.Current.CancellationToken);
      refresh = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
      await renewing.Task.WaitAsync(TestContext.Current.CancellationToken);
      if (renewalFirst)
      {
        releaseRenewal.TrySetResult();
        await refresh;
        Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
        releaseAuthentication.TrySetResult();
      }
      else
      {
        releaseAuthentication.TrySetResult();
        await authentication;
        releaseRenewal.TrySetResult();
      }
    }
    finally { releaseAuthentication.TrySetResult(); releaseRenewal.TrySetResult(); }
    await Task.WhenAll(authentication, refresh!);
    Assert.Equal("account-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("account-st", fixture.Persistence.Value!);
    Assert.DoesNotContain("new-st", fixture.Persistence.Value!);
    Assert.DoesNotContain("old-st", fixture.Persistence.Value!);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task NewerPendingAuthenticationFencesOlderSuccessWhenBothWaitedForRenewal(bool newerFirst)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var renewing = Gate();
    var releaseRenewal = Gate();
    var firstEntered = Gate();
    var secondEntered = Gate();
    var releaseFirst = Gate();
    var releaseSecond = Gate();
    fixture.Terminal.Renew = async _ =>
    {
      renewing.TrySetResult();
      await releaseRenewal.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath.Contains("/complete", StringComparison.Ordinal))
      {
        secondEntered.TrySetResult();
        await releaseSecond.Task.WaitAsync(TestContext.Current.CancellationToken);
        return UnchangedAuthentication("pending");
      }
      firstEntered.TrySetResult();
      await releaseFirst.Task.WaitAsync(TestContext.Current.CancellationToken);
      return WithCookies("obsolete-dt", "obsolete-st");
    };
    var refresh = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    Task? first = null;
    Task? second = null;
    try
    {
      await renewing.Task.WaitAsync(TestContext.Current.CancellationToken);
      first = fixture.Client.SendAsync(AuthenticationRequest("failed"), TestContext.Current.CancellationToken);
      second = fixture.Client.SendAsync(AuthenticationRequest("pending"), TestContext.Current.CancellationToken);
      releaseRenewal.TrySetResult();
      await Task.WhenAll(firstEntered.Task, secondEntered.Task).WaitAsync(TestContext.Current.CancellationToken);
      if (newerFirst) { releaseSecond.TrySetResult(); await second; releaseFirst.TrySetResult(); }
      else { releaseFirst.TrySetResult(); await first; releaseSecond.TrySetResult(); }
    }
    finally { releaseRenewal.TrySetResult(); releaseFirst.TrySetResult(); releaseSecond.TrySetResult(); }
    await Task.WhenAll(refresh, first!, second!);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", fixture.Persistence.Value!);
    Assert.DoesNotContain("obsolete-st", fixture.Persistence.Value!);
  }

  [Theory]
  [InlineData("failed", false)]
  [InlineData("failed", true)]
  [InlineData("mfa", false)]
  [InlineData("mfa", true)]
  [InlineData("pending", false)]
  [InlineData("pending", true)]
  public async Task NonReplacingAuthenticationPreservesLaterRenewalInBothCompletionOrders(string outcome, bool renewalFirst)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var authenticating = Gate();
    var renewing = Gate();
    var releaseAuthentication = Gate();
    var releaseRenewal = Gate();
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/my/identity")
        return Json(HttpStatusCode.OK, new { identity = new { id = "user-1" } });
      authenticating.TrySetResult();
      await releaseAuthentication.Task.WaitAsync(TestContext.Current.CancellationToken);
      return UnchangedAuthentication(outcome);
    };
    fixture.Terminal.Renew = async _ =>
    {
      renewing.TrySetResult();
      await releaseRenewal.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    var authentication = fixture.Client.SendAsync(AuthenticationRequest(outcome), TestContext.Current.CancellationToken);
    Task? refresh = null;
    try
    {
      await authenticating.Task.WaitAsync(TestContext.Current.CancellationToken);
      refresh = ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
      await renewing.Task.WaitAsync(TestContext.Current.CancellationToken);
      if (renewalFirst)
      {
        releaseRenewal.TrySetResult();
        await refresh;
        releaseAuthentication.TrySetResult();
      }
      else
      {
        releaseAuthentication.TrySetResult();
        if (outcome == "failed") await Assert.ThrowsAsync<VouchaApiException>(() => authentication);
        else await authentication;
        releaseRenewal.TrySetResult();
      }
    }
    finally { releaseAuthentication.TrySetResult(); releaseRenewal.TrySetResult(); }
    await refresh!;
    if (outcome == "failed") await Assert.ThrowsAsync<VouchaApiException>(() => authentication);
    else await authentication;
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", fixture.Persistence.Value!);
    Assert.DoesNotContain("old-st", fixture.Persistence.Value!);
    Assert.True(fixture.Store.Current.IsAuthenticated);
  }

  private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
  private static ApiRequest AuthenticationRequest(string outcome) => new(HttpMethod.Post,
      outcome == "pending" ? "/api/v1/auth/oauth/authorizations/flow/complete" : "/api/v1/auth/email-address/login");
  private static HttpResponseMessage UnchangedAuthentication(string outcome) => outcome switch
  {
    "failed" => Json(HttpStatusCode.BadRequest, new { code = "INVALID_LOGIN" }),
    "pending" => Json(HttpStatusCode.Accepted, new { status = "pending" }),
    _ => Json(HttpStatusCode.OK, new { mfa = new { required = true } }),
  };
}

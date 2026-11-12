using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  public static IEnumerable<object[]> CredentialTransitionCases()
  {
    foreach (var transition in new[] { "clear", "replace", "sign-out", "raw-logout", "auth-cookie" })
      foreach (var renewalFirst in new[] { false, true }) yield return [transition, renewalFirst];
#if DEBUG
    foreach (var renewalFirst in new[] { false, true }) yield return ["development", renewalFirst];
#endif
  }

  [Theory]
  [MemberData(nameof(CredentialTransitionCases))]
  public async Task CredentialTransitionsAbandonHeldPatchOnlyAfterDiscardOrReplacement(string transition, bool renewalFirst)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = Gate();
    var release = Gate();
    fixture.Terminal.Renew = async _ =>
    {
      entered.TrySetResult();
      await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    fixture.Terminal.Other = request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/auth/logout")
      {
        Assert.Contains(renewalFirst ? "st=new-st" : "st=old-st", request.Headers.GetValues("Cookie").Single());
        return Task.FromResult(Json(HttpStatusCode.OK, new { }));
      }
      return Task.FromResult(WithCookies("manual-dt", "manual-st"));
    };
    var renewal = BoundaryRotation(fixture);
    try
    {
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      if (renewalFirst)
      {
        release.TrySetResult();
        await renewal;
      }
      await ApplyBoundaryTransition(fixture, transition, TestContext.Current.CancellationToken);
    }
    finally { release.TrySetResult(); }
    if (!renewalFirst) await Assert.ThrowsAsync<UnauthorizedAccessException>(() => renewal);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    var replacement = transition is "replace" or "auth-cookie" or "development";
    Assert.Equal(replacement ? "manual-st" : null, fixture.Jar.CookieContainer.GetCookies(Origin)["st"]?.Value);
    using var restored = new SessionCookieJar(new CookieContainer(), Origin, fixture.Persistence);
    await restored.RestoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(replacement ? "manual-st" : null, restored.CookieContainer.GetCookies(Origin)["st"]?.Value);
    if (replacement) Assert.DoesNotContain("new-st", fixture.Persistence.Value!);
    else Assert.Null(fixture.Persistence.Value);
  }

  public static IEnumerable<object[]> AbortableCredentialTransitionCases()
  {
    foreach (var transition in new[] { "clear", "replace" })
      foreach (var renewalFirst in new[] { false, true }) yield return [transition, renewalFirst];
#if DEBUG
    foreach (var renewalFirst in new[] { false, true }) yield return ["development", renewalFirst];
#endif
  }

  [Theory]
  [MemberData(nameof(AbortableCredentialTransitionCases))]
  public async Task CanceledPreDiscardTransitionRetainsTheConsumedPatchReplacement(string transition, bool renewalFirst)
  {
    GatedPersistence? gated = null;
    using var fixture = new RecoveryFixture(wrap: inner => gated = new(inner));
    await fixture.SeedAsync();
    var entered = Gate();
    var release = Gate();
    var responseEntered = Gate();
    var responseRelease = Gate();
    fixture.Terminal.Renew = async _ =>
    {
      entered.TrySetResult();
      await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      return Rotated();
    };
    fixture.Terminal.Other = async _ =>
    {
      responseEntered.TrySetResult();
      await responseRelease.Task.WaitAsync(TestContext.Current.CancellationToken);
      return WithCookies(renewalFirst ? "new-dt" : "old-dt", renewalFirst ? "new-st" : "old-st");
    };
    Task<HttpResponseMessage>? attributes = null;
    if (!renewalFirst)
    {
      attributes = fixture.RawAsync("/attributes");
      await responseEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    }
    var renewal = BoundaryRotation(fixture);
    using var canceled = new CancellationTokenSource();
    try
    {
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      if (renewalFirst)
      {
        release.TrySetResult();
        await renewal;
        attributes = fixture.RawAsync("/attributes");
        await responseEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      }
      gated!.Block = true;
      responseRelease.TrySetResult();
      await gated.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      var pending = ApplyBoundaryTransition(fixture, transition, canceled.Token);
      Assert.False(pending.IsCompleted);
      canceled.Cancel();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
      Assert.Equal(renewalFirst ? "new-st" : "old-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    }
    finally { responseRelease.TrySetResult(); gated!.Release.TrySetResult(); release.TrySetResult(); }
    using var response = await attributes!;
    await renewal;
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", fixture.Persistence.Value!);
    Assert.DoesNotContain("old-st", fixture.Persistence.Value!);
    Assert.DoesNotContain("manual-st", fixture.Persistence.Value!);
  }

  public static IEnumerable<object[]> SupersededCredentialTransitionCases()
  {
    foreach (var transition in new[] { "sign-out", "clear", "replace" }) yield return [transition];
#if DEBUG
    yield return ["development"];
#endif
  }

  [Theory]
  [MemberData(nameof(SupersededCredentialTransitionCases))]
  public async Task SupersededTransitionAdmissionRetainsPendingRotationForTheNewerAuthenticationIntent(string transition)
  {
    GatedPersistence? gated = null;
    using var fixture = new RecoveryFixture(wrap: inner => gated = new(inner));
    await fixture.SeedAsync();
    var responseEntered = Gate();
    var responseRelease = Gate();
    var renewalEntered = Gate();
    var renewalRelease = Gate();
    var logoutRequests = 0;
    fixture.Terminal.Other = async request =>
    {
      if (request.RequestUri!.AbsolutePath == "/attributes")
      {
        responseEntered.TrySetResult();
        await responseRelease.Task.WaitAsync(TestContext.Current.CancellationToken);
        return WithCookies("old-dt", "old-st");
      }
      if (request.RequestUri.AbsolutePath == "/api/v1/auth/logout") logoutRequests++;
      return UnchangedAuthentication("pending");
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
    Task? pending = null;
    Task? newerIntent = null;
    try
    {
      await renewalEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      gated!.Block = true;
      responseRelease.TrySetResult();
      await gated.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      pending = ApplyBoundaryTransition(fixture, transition, TestContext.Current.CancellationToken);
      newerIntent = fixture.Client.SendAsync(AuthenticationRequest("pending"), TestContext.Current.CancellationToken);
      Assert.False(pending.IsCompleted);
      gated.Release.TrySetResult();
      if (transition == "sign-out") await pending;
      else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
      await newerIntent;
      Assert.Equal(0, logoutRequests);
    }
    finally { responseRelease.TrySetResult(); gated!.Release.TrySetResult(); renewalRelease.TrySetResult(); }
    using var response = await attributes;
    await renewal;
    Assert.Equal("new-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", fixture.Persistence.Value!);
  }

  private static Task BoundaryRotation(RecoveryFixture fixture) => fixture.Jar.RenewAsync(
      (dt, st, token) => fixture.Client.SendAsync<NativeSessionRefreshResponse>(
          VouchaApiEndpoints.RefreshSession(dt!, st) with { IsCoordinatedSessionRotation = true }, token),
      cancellationToken: TestContext.Current.CancellationToken);

  private static Task ApplyBoundaryTransition(RecoveryFixture fixture, string transition, CancellationToken token) => transition switch
  {
    "clear" => fixture.Jar.ClearAsync(token),
    "replace" => fixture.Jar.StoreSessionTokensAsync("manual-dt", "manual-st", TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1), token),
    "sign-out" => fixture.Store.SignOutAsync(token),
    "raw-logout" => fixture.Client.SendAsync(VouchaApiEndpoints.Logout(), token),
    "auth-cookie" => fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login"), token),
#if DEBUG
    "development" => fixture.Jar.StoreDevelopmentCookiesAsync("manual-dt", "manual-st", token),
#endif
    _ => throw new ArgumentOutOfRangeException(nameof(transition)),
  };

}

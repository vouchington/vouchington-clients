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
  public async Task UnauthorizedDecisionUsesCredentialsActuallyDispatchedAfterWaiting(bool waitingReplay)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var gate = new ReviewDispatchGate(waitingReplay ? 2 : 1,
        new SessionCookiePersistenceHandler(fixture.Jar, fixture.Terminal));
    using var http = new HttpClient(new ClientMetadataHandler(new(ClientPlatform.Windows, "1.0"), Origin,
        fixture.Jar, gate))
    { BaseAddress = Origin };
    var api = new VouchaApiClient(http);
    fixture.Terminal.Renew = async request =>
    {
      var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
      return body.RootElement.GetProperty("dt").GetString() == "old-dt"
          ? Rotated() : Rotated(dt: "second-dt", st: "second-st");
    };
    fixture.Terminal.Other = request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/my/identity")
        return Task.FromResult(Json(HttpStatusCode.OK, new { identity = new { id = "user-1" } }));
      var fresh = request.Headers.GetValues("Cookie").Single().Contains("st=second-st", StringComparison.Ordinal);
      return Task.FromResult(Json(!waitingReplay && fresh ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
          new { code = "AUTH_REQUIRED" }));
    };
    var call = api.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken);
    try
    {
      await Task.WhenAny(gate.Entered.Task, call).WaitAsync(TestContext.Current.CancellationToken);
      if (call.IsCompleted) await call;
      await gate.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      await ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, TestContext.Current.CancellationToken);
    }
    finally { gate.Release.TrySetResult(); }
    if (waitingReplay)
    {
      await Assert.ThrowsAsync<VouchaApiException>(() => call);
      Assert.False(fixture.Jar.HasDeviceCookie);
      Assert.False(fixture.Store.Current.IsAuthenticated);
      Assert.Null(fixture.Persistence.Value);
    }
    else
    {
      await call;
      Assert.Equal("second-st", fixture.Jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
      Assert.Contains("second-st", fixture.Persistence.Value!);
    }
    Assert.Equal(2, fixture.Terminal.RenewCount);
  }

  [Fact]
  public async Task TransportWithoutCookieDispatchSnapshotDoesNotInventRecoveryState()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Other = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" }));
    using var http = new HttpClient(new ClientMetadataHandler(new(ClientPlatform.Windows, "1.0"), Origin,
        fixture.Jar, fixture.Terminal))
    { BaseAddress = Origin };
    var api = new VouchaApiClient(http);
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => api.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.Equal("AUTH_REQUIRED", error.ErrorCode);
    Assert.Equal(0, fixture.Terminal.RenewCount);
    Assert.Contains("old-st", fixture.Persistence.Value!);
  }

  private sealed class ReviewDispatchGate(int ordinal, HttpMessageHandler inner) : DelegatingHandler(inner)
  {
    private int requests;
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/posts" && Interlocked.Increment(ref requests) == ordinal)
      {
        Entered.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken);
      }
      return await base.SendAsync(request, cancellationToken);
    }
  }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Fact]
  public async Task ConcurrentUnauthorizedAndForcedRefreshShareRotationAndPersistBeforeReplay()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Renew = async request =>
    {
      var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
      Assert.Equal("old-dt", JsonDocument.Parse(body).RootElement.GetProperty("dt").GetString());
      Assert.Equal("old-st", JsonDocument.Parse(body).RootElement.GetProperty("st").GetString());
      entered.SetResult();
      await release.Task;
      return Rotated();
    };
    var first = fixture.Client.SendAsync<JsonElement>(new ApiRequest(HttpMethod.Post, "/api/v1/posts")
    {
      Body = new { message = "repeatable" },
    }, TestContext.Current.CancellationToken);
    await entered.Task;
    var second = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/notifications") { Body = new { seen = true } },
        TestContext.Current.CancellationToken);
    var refresh = ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true,
        TestContext.Current.CancellationToken);
    release.SetResult();
    await Task.WhenAll(first, second, refresh);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.True(fixture.Store.Current.IsAuthenticated);
    Assert.Contains("new-st", fixture.Persistence.Value!);
    Assert.Equal(2, fixture.Terminal.SuccessfulReplays);
    Assert.Equal("repeatable", fixture.Terminal.ReplayedBody.GetProperty("message").GetString());
  }

  [Theory]
  [InlineData("SESSION_RELOGIN_REQUIRED")]
  [InlineData("SESSION_REFRESH_REUSED")]
  [InlineData("SESSION_USER_AGENT_MISMATCH")]
  [InlineData("SESSION_JA4_MISMATCH")]
  public async Task DefinitiveRenewalFailureClearsCookies(string code)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Renew = _ => Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code }));
    var exception = await Assert.ThrowsAsync<VouchaApiException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.Equal(code, exception.ErrorCode);
    Assert.Null(fixture.Persistence.Value);
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Equal(1, fixture.Terminal.RenewCount);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task TransientRenewalFailurePreservesCookies(bool networkFailure)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var before = fixture.Persistence.Value;
    fixture.Terminal.Renew = _ => networkFailure
        ? Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))
        : Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, new { code = "UNAVAILABLE" }));
    await Assert.ThrowsAnyAsync<HttpRequestException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.Equal(before, fixture.Persistence.Value);
    Assert.True(fixture.Jar.HasDeviceCookie);
  }

  [Theory]
  [InlineData(null, "new-dt", "new-st", 60)]
  [InlineData("user-1", "", "new-st", 60)]
  [InlineData("user-1", "new-dt", "", 60)]
  [InlineData("user-1", "new-dt", "new-st", 0)]
  public async Task InvalidRenewalNeverAuthenticates(string? uid, string dt, string st, int dte)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    fixture.Terminal.Renew = _ => Task.FromResult(Rotated(uid, dt, st, dte));
    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Client.SendAsync(
        new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken));
    Assert.False(fixture.Jar.HasDeviceCookie);
    Assert.Null(fixture.Persistence.Value);
  }

  [Fact]
  public async Task CanceledWaiterDoesNotCancelSharedRotation()
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Renew = async _ => { entered.SetResult(); await release.Task; return Rotated(); };
    using var cancellation = new CancellationTokenSource();
    var canceled = ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true, cancellation.Token);
    await entered.Task;
    var survivor = ((IForcedSessionRefreshStore)fixture.Store).RefreshAsync(true,
        TestContext.Current.CancellationToken);
    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
    release.SetResult();
    await survivor;
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.True(fixture.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task ExpiredSessionCookieRestartsWithDeviceOnlyRefreshAndServerExpiry()
  {
    var persistence = new TestSessionCookiePersistence();
    await persistence.WriteAsync(JsonSerializer.Serialize(new
    {
      Cookies = new object[]
      {
        new { Name = "dt", Value = "old-dt", ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(3) },
        new { Name = "st", Value = "old-st", ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1) },
      },
    }), TestContext.Current.CancellationToken);
    using var fixture = new RecoveryFixture(persistence);
    fixture.Terminal.Renew = async request =>
    {
      var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
      Assert.Equal("old-dt", body.RootElement.GetProperty("dt").GetString());
      Assert.False(body.RootElement.TryGetProperty("st", out _));
      return Rotated(dte: 120);
    };
    await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"),
        TestContext.Current.CancellationToken);
    var expiry = fixture.Jar.CookieContainer.GetCookies(Origin)["dt"]!.Expires;
    Assert.InRange(expiry.ToUniversalTime(), DateTime.UtcNow.AddSeconds(110), DateTime.UtcNow.AddSeconds(121));
    Assert.Contains("new-st", persistence.Value!);
  }

  private static readonly Uri Origin = new("https://voucha.test");

  private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
  {
    Content = JsonContent.Create(body),
  };

  private static HttpResponseMessage Rotated(
      string? uid = "user-1", string dt = "new-dt", string st = "new-st", int dte = 120, bool secure = true) =>
      Json(HttpStatusCode.OK, new
      {
        session = new { dt, st, dte, ste = 60, secure, uid, session = new { uid, client = "web" } },
      });

  private sealed class RecoveryFixture : IDisposable
  {
    private readonly HttpClient http;
    public RecoveryFixture(TestSessionCookiePersistence? persistence = null,
        Func<TestSessionCookiePersistence, ISessionCookiePersistence>? wrap = null)
    {
      Persistence = persistence ?? new TestSessionCookiePersistence();
      Jar = new(new CookieContainer(), Origin, wrap?.Invoke(Persistence) ?? Persistence);
      Terminal = new(Persistence);
      http = new(new ClientMetadataHandler(new(ClientPlatform.Windows, "1.0"), Origin, Jar,
          new SessionCookiePersistenceHandler(Jar, Terminal)))
      { BaseAddress = Origin };
      Client = new(http);
      Store = new(Client, Jar);
    }
    public TestSessionCookiePersistence Persistence { get; }
    public SessionCookieJar Jar { get; }
    public RecoveryTerminal Terminal { get; }
    public VouchaApiClient Client { get; }
    public CookieSessionStore Store { get; }
    public void SetUserAgent(string value) => http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", value);
    public Task<HttpResponseMessage> RawAsync(string path) => http.GetAsync(path, TestContext.Current.CancellationToken);
    public Task SeedAsync() => Jar.StoreSessionTokensAsync("old-dt", "old-st",
        TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    public void Dispose() { http.Dispose(); Jar.Dispose(); }
  }

  private sealed class RecoveryTerminal(TestSessionCookiePersistence persistence) : HttpMessageHandler
  {
    public Func<HttpRequestMessage, Task<HttpResponseMessage>> Renew { get; set; } = _ => Task.FromResult(Rotated());
    public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Other { get; set; }
    public int RenewCount;
    public int SuccessfulReplays;
    public JsonElement ReplayedBody;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/session")
      {
        Interlocked.Increment(ref RenewCount);
        return await Renew(request);
      }
      if (Other is not null) return await Other(request);
      if (request.RequestUri.AbsolutePath == "/api/v1/my/identity")
        return Json(HttpStatusCode.OK, new { identity = new { id = "user-1", username = "alice" } });
      if (request.Headers.TryGetValues("Cookie", out var values) && values.Single().Contains("st=new-st", StringComparison.Ordinal))
      {
        Assert.Contains("new-st", persistence.Value!);
        Interlocked.Increment(ref SuccessfulReplays);
        if (request.Content is not null)
        {
          var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement;
          if (request.RequestUri.AbsolutePath == "/api/v1/posts") ReplayedBody = body.Clone();
          else Assert.True(body.GetProperty("seen").GetBoolean());
        }
        return Json(HttpStatusCode.OK, new { ok = true });
      }
      return Json(HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" });
    }
  }
}

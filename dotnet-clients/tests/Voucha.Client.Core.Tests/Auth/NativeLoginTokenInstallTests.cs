using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeLoginTokenInstallTests
{
  private static readonly Uri ApiBaseUrl = new("https://api.test");

  private static string Jwt(string name, TimeSpan? lifetime = null)
  {
    var exp = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)).ToUnixTimeSeconds();
    var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($$"""{"exp":{{exp}}}"""))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    return $"h.{payload}.{name}";
  }

  private static string LoginBody(string dt, string st) =>
      """{"did":"d","sid":"s","uid":"u","dt":{"token":"DT","payload":{"did":"d"}},"st":{"token":"ST","payload":{"did":"d","sid":"s","uid":"u"}}}"""
          .Replace("DT", dt, StringComparison.Ordinal)
          .Replace("ST", st, StringComparison.Ordinal);

  [Fact]
  public async Task InstallsBodyTokensAsPersistedCookiesWithJwtExpiry()
  {
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    var jar = new SessionCookieJar(container, ApiBaseUrl, persistence);
    var device = Jwt("device");
    var session = Jwt("session", TimeSpan.FromMinutes(10));

    await jar.InstallNativeLoginTokensAsync(device, session, TestContext.Current.CancellationToken);

    var cookies = container.GetCookies(ApiBaseUrl);
    Assert.Equal(device, cookies["dt"]?.Value);
    Assert.Equal(session, cookies["st"]?.Value);
    Assert.True(cookies["st"]!.Expires.ToUniversalTime() < DateTime.UtcNow.AddMinutes(11));
    Assert.True(cookies["dt"]!.HttpOnly);
    Assert.Equal(1, persistence.WriteCount);
    Assert.Contains(session, persistence.Value, StringComparison.Ordinal);
  }

  [Fact]
  public async Task LeavesTokensAlreadyPresentFromSetCookieUntouched()
  {
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    var jar = new SessionCookieJar(container, ApiBaseUrl, persistence);
    container.Add(ApiBaseUrl, new Cookie("dt", "fixture-device-jwt", "/") { Secure = true });
    container.Add(ApiBaseUrl, new Cookie("st", "fixture-session-jwt", "/") { Secure = true });
    var revision = jar.CredentialRevision;

    await jar.InstallNativeLoginTokensAsync(
        "fixture-device-jwt", "fixture-session-jwt", TestContext.Current.CancellationToken);

    Assert.Equal(revision, jar.CredentialRevision);
    Assert.Equal(0, persistence.WriteCount);
  }

  [Theory]
  [InlineData("not-a-jwt")]
  [InlineData("a.!!!.c")]
  [InlineData("")]
  public async Task RejectsTokensWithoutASignedExpiryAndInstallsNothing(string bad)
  {
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    var jar = new SessionCookieJar(container, ApiBaseUrl, persistence);

    await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        jar.InstallNativeLoginTokensAsync(Jwt("device"), bad, TestContext.Current.CancellationToken));

    Assert.Empty(container.GetCookies(ApiBaseUrl));
    Assert.Equal(0, persistence.WriteCount);
  }

  [Fact]
  public async Task RejectsExpiredTokens()
  {
    var jar = new SessionCookieJar(new CookieContainer(), ApiBaseUrl, new TestSessionCookiePersistence());

    await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        jar.InstallNativeLoginTokensAsync(
            Jwt("device", TimeSpan.FromSeconds(-30)), Jwt("session"), TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task VerifyEmailOtpInstallsBodyTokensBeforeRefreshingTheSession()
  {
    var container = new CookieContainer();
    var device = Jwt("device");
    var session = Jwt("session");
    var handler = new RecordingHandler(LoginBody(device, session), device, session);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = ApiBaseUrl });
    var store = new CookieSessionStore(
        client, new SessionCookieJar(container, ApiBaseUrl, new TestSessionCookiePersistence()));
    var service = new AuthenticationService(client, store, new NoPasskeys());

    var challenge = await service.VerifyEmailOtpAsync(
        "a@example.com", "ABCDEFGH23", TestContext.Current.CancellationToken);

    Assert.Null(challenge);
    Assert.Equal("/api/v1/auth/native/email-address/login", handler.Paths[0]);
    Assert.Equal("/api/v1/session", handler.Paths[1]);
    Assert.True(handler.PostLoginRequestCarriedTokens);
  }

  [Fact]
  public async Task MfaResponseInstallsNoCredentials()
  {
    var container = new CookieContainer();
    var handler = new RecordingHandler("""{"mfa_required":true,"login_attempt_id":"attempt-1"}""");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = ApiBaseUrl });
    var store = new CookieSessionStore(
        client, new SessionCookieJar(container, ApiBaseUrl, new TestSessionCookiePersistence()));
    var service = new AuthenticationService(client, store, new NoPasskeys());

    var challenge = await service.VerifyEmailOtpAsync(
        "a@example.com", "ABCDEFGH23", TestContext.Current.CancellationToken);

    Assert.Equal("attempt-1", challenge?.LoginAttemptId);
    Assert.Empty(container.GetCookies(ApiBaseUrl));
    Assert.Single(handler.Paths);
  }

  private sealed class NoPasskeys : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(
        PasskeyAuthenticationOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class RecordingHandler(string loginBody, string device = "", string session = "") : HttpMessageHandler
  {
    private const string RefreshBody =
        """{"session":{"dt":"rotated-dt","st":"rotated-st","dte":3600,"ste":900,"secure":true,"uid":"user-1","session":{"uid":"user-1","client":"dotnet"}}}""";

    public List<string> Paths { get; } = [];

    public bool PostLoginRequestCarriedTokens { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      Paths.Add(path);
      if (path == "/api/v1/session" && request.Content is { } content &&
          content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() is var sent &&
          sent.Contains(device, StringComparison.Ordinal) && sent.Contains(session, StringComparison.Ordinal))
        PostLoginRequestCarriedTokens = true;
      var body = path switch
      {
        "/api/v1/auth/native/email-address/login" => loginBody,
        "/api/v1/session" => RefreshBody,
        _ => """{"identity":{"id":"user-1","username":"alice","roles":["user"],"email_address":"alice@example.com","membership_plan":"free"}}""",
      };
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body),
        RequestMessage = request,
      });
    }
  }
}

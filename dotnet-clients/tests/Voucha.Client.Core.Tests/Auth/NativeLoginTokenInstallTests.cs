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

    await jar.InstallNativeLoginTokensAsync(device, session, cancellationToken: TestContext.Current.CancellationToken);

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
        "fixture-device-jwt", "fixture-session-jwt", cancellationToken: TestContext.Current.CancellationToken);

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
        jar.InstallNativeLoginTokensAsync(Jwt("device"), bad, cancellationToken: TestContext.Current.CancellationToken));

    Assert.Empty(container.GetCookies(ApiBaseUrl));
    Assert.Equal(0, persistence.WriteCount);
  }

  [Fact]
  public async Task RejectsExpiredTokens()
  {
    var jar = new SessionCookieJar(new CookieContainer(), ApiBaseUrl, new TestSessionCookiePersistence());

    await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        jar.InstallNativeLoginTokensAsync(
            Jwt("device", TimeSpan.FromSeconds(-30)), Jwt("session"), cancellationToken: TestContext.Current.CancellationToken));
  }

  private sealed class Stack : IDisposable
  {
    private const string RefreshBody =
        """{"session":{"dt":"rotated-dt","st":"rotated-st","dte":3600,"ste":900,"secure":true,"uid":"user-1","session":{"uid":"user-1","client":"dotnet"}}}""";
    private readonly HttpClient http;

    public Stack(string loginBody, Func<Task>? beforeLoginResponse = null)
    {
      Container = new CookieContainer();
      Persistence = new TestSessionCookiePersistence();
      Jar = new SessionCookieJar(Container, ApiBaseUrl, Persistence);
      Terminal = new Terminal(loginBody, RefreshBody, beforeLoginResponse);
      http = new HttpClient(new ClientMetadataHandler(
          new ClientMetadata(ClientPlatform.Windows, "1.0"), ApiBaseUrl, Jar,
          new SessionCookiePersistenceHandler(Jar, Terminal)))
      { BaseAddress = ApiBaseUrl };
      Client = new VouchaApiClient(http);
      Store = new CookieSessionStore(Client, Jar);
      Service = new AuthenticationService(Client, Store, new NoPasskeys());
    }

    public CookieContainer Container { get; }
    public TestSessionCookiePersistence Persistence { get; }
    public SessionCookieJar Jar { get; }
    public Terminal Terminal { get; }
    public VouchaApiClient Client { get; }
    public CookieSessionStore Store { get; }
    public AuthenticationService Service { get; }

    public void Dispose()
    {
      http.Dispose();
      Jar.Dispose();
    }
  }

  [Fact]
  public async Task VerifyEmailOtpInstallsBodyTokensBeforeRefreshingTheSession()
  {
    var device = Jwt("device");
    var session = Jwt("session");
    using var stack = new Stack(LoginBody(device, session));

    var challenge = await stack.Service.VerifyEmailOtpAsync(
        "a@example.com", "ABCDEFGH23", TestContext.Current.CancellationToken);

    Assert.Null(challenge);
    // The first PATCH is the anonymous session bootstrap; the last one is the post-login refresh.
    Assert.Equal(
        ["/api/v1/session", "/api/v1/auth/native/email-address/login", "/api/v1/session"],
        stack.Terminal.Paths.Take(3));
    Assert.Contains(device, stack.Terminal.RefreshBody, StringComparison.Ordinal);
    Assert.Contains(session, stack.Terminal.RefreshBody, StringComparison.Ordinal);
    Assert.True(stack.Store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task MfaResponseInstallsNoCredentials()
  {
    using var stack = new Stack("""{"mfa_required":true,"login_attempt_id":"attempt-1"}""");

    var challenge = await stack.Service.VerifyEmailOtpAsync(
        "a@example.com", "ABCDEFGH23", TestContext.Current.CancellationToken);

    Assert.Equal("attempt-1", challenge?.LoginAttemptId);
    Assert.Equal(["/api/v1/session", "/api/v1/auth/native/email-address/login"], stack.Terminal.Paths);
    Assert.Equal("rotated-dt", stack.Container.GetCookies(ApiBaseUrl)["dt"]?.Value);
  }

  [Fact]
  public async Task LoginResponseSupersededBySignOutDoesNotRestoreCredentials()
  {
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var stale = Jwt("stale-device");
    using var stack = new Stack(LoginBody(stale, Jwt("stale-session")), async () =>
    {
      entered.TrySetResult();
      await release.Task;
    });

    var login = stack.Service.VerifyEmailOtpAsync(
        "a@example.com", "ABCDEFGH23", TestContext.Current.CancellationToken);
    await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
    await stack.Jar.ClearAsync(TestContext.Current.CancellationToken);
    release.SetResult();

    try { await login; }
    catch (UnauthorizedAccessException) { }
    Assert.DoesNotContain(stale, stack.Terminal.RefreshBody, StringComparison.Ordinal);
    Assert.DoesNotContain(stale, stack.Persistence.Value ?? "", StringComparison.Ordinal);
    Assert.NotEqual(stale, stack.Container.GetCookies(ApiBaseUrl)["dt"]?.Value);
  }

  [Fact]
  public async Task LoginResponseSupersededByNewerAuthenticationDoesNotOverwriteIt()
  {
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var stale = Jwt("stale-device");
    using var stack = new Stack(LoginBody(stale, Jwt("stale-session")), async () =>
    {
      entered.TrySetResult();
      await release.Task;
    });

    var login = stack.Service.VerifyEmailOtpAsync(
        "a@example.com", "ABCDEFGH23", TestContext.Current.CancellationToken);
    await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
    var unusedMarker = Jwt("stale-device");
    var newer = Jwt("newer-device");
    await stack.Jar.StoreSessionTokensAsync(
        newer, Jwt("newer-session"), TimeSpan.FromHours(1), TimeSpan.FromHours(1),
        TestContext.Current.CancellationToken);
    stack.Jar.BeginAuthentication();
    release.SetResult();

    try { await login; }
    catch (UnauthorizedAccessException) { }
    Assert.DoesNotContain(stale, stack.Terminal.RefreshBody, StringComparison.Ordinal);
    Assert.Contains(newer, stack.Terminal.RefreshBody, StringComparison.Ordinal);
  }

  private sealed class NoPasskeys : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(
        PasskeyAuthenticationOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class Terminal(string loginBody, string refreshBody, Func<Task>? beforeLoginResponse)
      : HttpMessageHandler
  {
    public List<string> Paths { get; } = [];

    public string RefreshBody { get; private set; } = "";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      Paths.Add(path);
      if (path == "/api/v1/auth/native/email-address/login" && beforeLoginResponse is not null)
        await beforeLoginResponse().ConfigureAwait(false);
      if (path == "/api/v1/session" && request.Content is { } content)
        RefreshBody = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      var body = path switch
      {
        "/api/v1/auth/native/email-address/login" => loginBody,
        "/api/v1/session" => refreshBody,
        _ => """{"identity":{"id":"user-1","username":"alice","roles":["user"],"email_address":"alice@example.com","membership_plan":"free"}}""",
      };
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body),
        RequestMessage = request,
      };
    }
  }
}

using System.Net;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class SessionCookieJarTests
{
  private static readonly Uri ApiBaseUrl = new("https://api.test");

  [Fact]
  public async Task RestoresPersistedSessionCookies()
  {
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    await persistence.WriteAsync(
        """{"Cookies":[{"Name":"dt","Value":"device"},{"Name":"st","Value":"session"}]}""",
        TestContext.Current.CancellationToken);
    var jar = new SessionCookieJar(container, ApiBaseUrl, persistence);

    await jar.RestoreAsync(TestContext.Current.CancellationToken);

    var header = container.GetCookieHeader(ApiBaseUrl);
    Assert.Contains("dt=device", header, StringComparison.Ordinal);
    Assert.Contains("st=session", header, StringComparison.Ordinal);
    Assert.All(container.GetCookies(ApiBaseUrl).Cast<Cookie>(), cookie =>
    {
      Assert.True(cookie.HttpOnly);
      Assert.True(cookie.Secure);
    });
  }

  [Fact]
  public async Task RestoresLocalhostCookiesWithoutExplicitDomain()
  {
    var baseUrl = new Uri("http://localhost:2999");
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    await persistence.WriteAsync(
        """{"Cookies":[{"Name":"dt","Value":"device"},{"Name":"st","Value":"session"}]}""",
        TestContext.Current.CancellationToken);
    var jar = new SessionCookieJar(container, baseUrl, persistence);

    await jar.RestoreAsync(TestContext.Current.CancellationToken);

    Assert.Contains("dt=device", container.GetCookieHeader(baseUrl), StringComparison.Ordinal);
    Assert.Contains("st=session", container.GetCookieHeader(baseUrl), StringComparison.Ordinal);
  }

  [Fact]
  public async Task RestoresFromPersistenceOnlyOnce()
  {
    var persistence = new TestSessionCookiePersistence();
    await persistence.WriteAsync(
        """{"Cookies":[{"Name":"dt","Value":"device"},{"Name":"st","Value":"session"}]}""",
        TestContext.Current.CancellationToken);
    var jar = new SessionCookieJar(new CookieContainer(), ApiBaseUrl, persistence);

    await jar.RestoreAsync(TestContext.Current.CancellationToken);
    await jar.RestoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(1, persistence.ReadCount);
  }

  [Fact]
  public async Task PersistsOnlyCurrentSessionCookiesForOrigin()
  {
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    container.Add(ApiBaseUrl, new Cookie("dt", "device", "/", "api.test"));
    container.Add(ApiBaseUrl, new Cookie("st", "session", "/", "api.test"));
    container.Add(new Uri("https://other.test"), new Cookie("dt", "other", "/", "other.test"));
    container.Add(ApiBaseUrl, new Cookie("unrelated", "value", "/", "api.test"));
    var jar = new SessionCookieJar(container, ApiBaseUrl, persistence);

    await jar.PersistAsync(TestContext.Current.CancellationToken);

    Assert.Equal(
        """{"Cookies":[{"Name":"dt","Value":"device"},{"Name":"st","Value":"session"}]}""",
        persistence.Value);
  }

  [Fact]
  public async Task BootstrapTokenExpirationsSurvivePersistenceAndRestore()
  {
    var persistence = new TestSessionCookiePersistence();
    var jar = new SessionCookieJar(new CookieContainer(), ApiBaseUrl, persistence);

    await jar.StoreSessionTokensAsync(
        "device",
        "session",
        TimeSpan.FromDays(30),
        TimeSpan.FromDays(1),
        TestContext.Current.CancellationToken);
    var restoredContainer = new CookieContainer();
    var restoredJar = new SessionCookieJar(restoredContainer, ApiBaseUrl, persistence);
    await restoredJar.RestoreAsync(TestContext.Current.CancellationToken);

    var cookies = restoredContainer.GetCookies(ApiBaseUrl);
    Assert.True(cookies["dt"]!.Expires.ToUniversalTime() > DateTime.UtcNow.AddDays(29));
    Assert.True(cookies["st"]!.Expires.ToUniversalTime() > DateTime.UtcNow.AddHours(23));
  }

  [Fact]
  public async Task SkipsPersistenceWhenSessionCookiesAreUnchanged()
  {
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    container.Add(ApiBaseUrl, new Cookie("dt", "device", "/", "api.test"));
    container.Add(ApiBaseUrl, new Cookie("st", "session", "/", "api.test"));
    var jar = new SessionCookieJar(container, ApiBaseUrl, persistence);

    await jar.PersistAsync(TestContext.Current.CancellationToken);
    await jar.PersistAsync(TestContext.Current.CancellationToken);

    Assert.Equal(1, persistence.WriteCount);
  }

  [Fact]
  public async Task ClearsPersistenceForCorruptPayloadsAndEmptyJar()
  {
    var persistence = new TestSessionCookiePersistence();
    await persistence.WriteAsync("not json", TestContext.Current.CancellationToken);
    var jar = new SessionCookieJar(new CookieContainer(), ApiBaseUrl, persistence);

    await jar.RestoreAsync(TestContext.Current.CancellationToken);
    await jar.PersistAsync(TestContext.Current.CancellationToken);

    Assert.Null(persistence.Value);
    Assert.Equal(2, persistence.ClearCount);
  }

  [Fact]
  public async Task ClearsMemoryAndPersistence()
  {
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    container.Add(ApiBaseUrl, new Cookie("dt", "device", "/", "api.test"));
    container.Add(ApiBaseUrl, new Cookie("st", "session", "/", "api.test"));
    var jar = new SessionCookieJar(container, ApiBaseUrl, persistence);
    await jar.PersistAsync(TestContext.Current.CancellationToken);

    await jar.ClearAsync(TestContext.Current.CancellationToken);

    Assert.Empty(container.GetCookies(ApiBaseUrl).Cast<Cookie>());
    Assert.Null(persistence.Value);
  }

  [Fact]
  public async Task HandlerRestoresBeforeRequestAndPersistsSetCookieResponses()
  {
    var container = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    await persistence.WriteAsync(
        """{"Cookies":[{"Name":"dt","Value":"device"},{"Name":"st","Value":"session"}]}""",
        TestContext.Current.CancellationToken);
    var inner = new CapturingSetCookieHandler(container);
    using var handler = new SessionCookiePersistenceHandler(
        new SessionCookieJar(container, ApiBaseUrl, persistence),
        inner);
    using var client = new HttpClient(handler) { BaseAddress = ApiBaseUrl };

    using var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);

    Assert.Equal("dt=device; st=session", inner.RestoredCookieHeader);
    Assert.Contains("\"device-2\"", persistence.Value, StringComparison.Ordinal);
    Assert.Contains("\"session-2\"", persistence.Value, StringComparison.Ordinal);
  }

  private sealed class CapturingSetCookieHandler : HttpMessageHandler
  {
    private readonly CookieContainer cookieContainer;

    public CapturingSetCookieHandler(CookieContainer cookieContainer) =>
        this.cookieContainer = cookieContainer;

    public string? RestoredCookieHeader { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      RestoredCookieHeader = cookieContainer.GetCookieHeader(ApiBaseUrl);
      var response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
      response.Headers.Add("Set-Cookie", "dt=device-2; Path=/; HttpOnly; Secure");
      response.Headers.Add("Set-Cookie", "st=session-2; Path=/; HttpOnly; Secure");
      return Task.FromResult(response);
    }
  }
}

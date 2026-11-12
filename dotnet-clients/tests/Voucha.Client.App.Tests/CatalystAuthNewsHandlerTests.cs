#if DEBUG
using System.Net;
using System.Net.Http.Json;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class CatalystAuthNewsHandlerTests
{
  private const string Tokens = "/api/v1/auth/email-address/tokens";
  private const string Login = "/api/v1/auth/email-address/login";
  private const string Identity = "/api/v1/my/identity";
  private const string News = "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=article";

  [Fact]
  public async Task BootstrapNeverAuthenticatesAndLoginRequiresAcceptedChallenge()
  {
    using var client = Client();
    using var anonymous = await client.GetAsync(Identity, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    using var bootstrap = await client.PatchAsJsonAsync("/api/v1/session", new { }, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
    using var identity = await client.GetAsync(Identity, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, identity.StatusCode);
    using var login = await client.PostAsJsonAsync(Login, LoginBody(), TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    using var news = await client.GetAsync(News, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.BadRequest, news.StatusCode);
  }

  [Theory]
  [InlineData("wrong@example.test", CatalystAuthNewsLaunch.TurnstileToken)]
  [InlineData(CatalystAuthNewsLaunch.Email, "wrong-token")]
  public async Task WrongEmailOrCaptchaCannotCreateChallenge(string email, string captcha)
  {
    using var client = Client();
    using var tokens = await client.PostAsJsonAsync(Tokens,
        new RequestEmailOtpBody(email, captcha), TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.BadRequest, tokens.StatusCode);
    using var login = await client.PostAsJsonAsync(Login, LoginBody(), TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    using var identity = await client.GetAsync(Identity, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, identity.StatusCode);
  }

  [Fact]
  public async Task AcceptedChallengeRejectsWrongCodeAndOnlySuccessfulLoginExposesIdentityAndNews()
  {
    using var client = Client();
    await RequestChallengeAsync(client);
    using var wrong = await client.PostAsJsonAsync(Login,
        new VerifyEmailOtpBody(CatalystAuthNewsLaunch.Email, "WRONGCODE1"), TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    using var before = await client.GetAsync(Identity, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, before.StatusCode);
    using var accepted = await client.PostAsJsonAsync(Login, LoginBody(), TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    var auth = await accepted.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
    Assert.False(auth!.MfaRequired);
    Assert.Equal(2, accepted.Headers.GetValues("Set-Cookie").Count());
    Assert.Equal(ApiFixtureLoader.LoadResponse("swift.my.identity.default"),
        await client.GetStringAsync(Identity, TestContext.Current.CancellationToken));
    Assert.Equal(ApiFixtureLoader.LoadResponse("web.rss-feed-items.feed.default"),
        await client.GetStringAsync(News, TestContext.Current.CancellationToken));
  }

  [Theory]
  [InlineData("GET", "/api/v1/feeds/rss_feed_items/any?limit=25&media_type=article")]
  [InlineData("GET", "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video")]
  [InlineData("POST", News)]
  [InlineData("GET", "https://voucha.ai/api/v1/my/identity")]
  [InlineData("GET", "http://ui-testing.voucha.invalid/api/v1/my/identity")]
  [InlineData("GET", "https://ui-testing.voucha.invalid:8443/api/v1/my/identity")]
  [InlineData("GET", "/api/v1/my/identity?unexpected=true")]
  [InlineData("DELETE", "/api/v1/rss-feed-items/item-1/read")]
  public async Task AuthenticatedBackendRejectsWrongHostMethodAndUnsupportedNewsRequests(string method, string uri)
  {
    using var client = Client();
    await RequestChallengeAsync(client);
    using var login = await client.PostAsJsonAsync(Login, LoginBody(), TestContext.Current.CancellationToken);
    using var request = new HttpRequestMessage(new HttpMethod(method), uri);
    using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task FreshBackendCannotInheritAnotherHandlersAuthentication()
  {
    using var first = Client();
    await RequestChallengeAsync(first);
    using var login = await first.PostAsJsonAsync(Login, LoginBody(), TestContext.Current.CancellationToken);
    using var second = Client();
    using var identity = await second.GetAsync(Identity, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, identity.StatusCode);
  }

  private static HttpClient Client() => new(new CatalystAuthNewsHandler())
  { BaseAddress = new Uri("https://ui-testing.voucha.invalid") };
  private static VerifyEmailOtpBody LoginBody() => new(CatalystAuthNewsLaunch.Email, CatalystAuthNewsLaunch.Code);
  private static async Task RequestChallengeAsync(HttpClient client)
  {
    using var response = await client.PostAsJsonAsync(Tokens,
        new RequestEmailOtpBody(CatalystAuthNewsLaunch.Email, CatalystAuthNewsLaunch.TurnstileToken),
        TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }
}
#endif

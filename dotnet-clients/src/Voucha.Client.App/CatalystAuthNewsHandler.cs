#if DEBUG
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Voucha.Client.App;

internal sealed class CatalystAuthNewsHandler : HttpMessageHandler
{
  private readonly string identity;
  // Deliberate unchanged UI replay: producer fixture uses limit=25 and video items
  // within an article feed. This is not an article-filter or response-contract parity test.
  private readonly string news;
  // Native login fixture shape; the two body tokens are re-minted as JWT-shaped credentials with a
  // future exp because the client refuses an installed token it cannot read an expiry from.
  private readonly JsonObject nativeLogin;
  private readonly string deviceToken = Jwt();
  private readonly string sessionToken = Jwt();
  private readonly string userId;
  private bool requested;
  private bool authenticated;
  private readonly object gate = new();

  public CatalystAuthNewsHandler(string? contractRoot = null)
  {
    identity = CatalystAuthNewsLaunch.ReadFixture(
        "swift.my.identity.default", "GET", "/api/v1/my/identity", contractRoot);
    news = CatalystAuthNewsLaunch.ReadFixture(
        "web.rss-feed-items.feed.default", "GET", "/api/v1/feeds/rss_feed_items/any", contractRoot);
    nativeLogin = (JsonObject)JsonNode.Parse(CatalystAuthNewsLaunch.ReadFixture(
        "native.auth.email-address.login.authenticated", "POST", "/api/v1/auth/native/email-address/login",
        contractRoot))!;
    using var document = JsonDocument.Parse(identity);
    userId = document.RootElement.GetProperty("identity").GetProperty("id").GetString()
        ?? throw new InvalidOperationException("Identity fixture has no user id.");
  }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    using var document = JsonDocument.Parse(body);
    lock (gate) return Respond(request, document.RootElement);
  }

  private HttpResponseMessage Respond(HttpRequestMessage request, JsonElement body)
  {
    if (request.RequestUri?.Host != "ui-testing.voucha.invalid" ||
        request.RequestUri.Scheme != Uri.UriSchemeHttps || !request.RequestUri.IsDefaultPort ||
        request.RequestUri.UserInfo.Length != 0)
      return Json("{}", HttpStatusCode.BadRequest);
    var path = request.RequestUri.AbsolutePath;
    if (path != "/api/v1/feeds/rss_feed_items/any" && request.RequestUri.Query.Length != 0)
      return Json("{}", HttpStatusCode.BadRequest);
    if (request.Method == HttpMethod.Post && path == "/api/v1/auth/native/email-address/tokens" &&
        Text(body, "emailAddress") == CatalystAuthNewsLaunch.Email &&
        Text(body, "cfTurnstileResponse") == CatalystAuthNewsLaunch.TurnstileToken)
    {
      requested = true;
      return Json("{}");
    }
    if (request.Method == HttpMethod.Post && path == "/api/v1/auth/native/email-address/login" && requested &&
        Text(body, "emailAddress") == CatalystAuthNewsLaunch.Email && Text(body, "token") == CatalystAuthNewsLaunch.Code)
    {
      authenticated = true;
      var issued = (JsonObject)nativeLogin.DeepClone();
      issued["dt"]!["token"] = deviceToken;
      issued["st"]!["token"] = sessionToken;
      return Json(issued.ToJsonString());
    }
    if (request.Method == HttpMethod.Get && path == "/api/v1/my/identity")
      return HasIssuedSession(request) ? Json(identity) : Json("{}", HttpStatusCode.Unauthorized);
    // Post-login rotation must return the same authenticated user, as the producer does.
    if (request.Method == HttpMethod.Patch && path == "/api/v1/session")
      return Json(HasIssuedSession(request)
          ? "{\"session\":{\"dt\":\"" + deviceToken + "\",\"st\":\"" + sessionToken + "\",\"dte\":3600,\"ste\":3600,\"uid\":" +
            JsonSerializer.Serialize(userId) + ""","session":{"uid":""" + JsonSerializer.Serialize(userId) + "}}}"
          : """{"session":{"dt":"catalyst-bootstrap-device","st":"catalyst-bootstrap-session","dte":3600,"ste":3600}}""");
    if (request.Method == HttpMethod.Get && path == "/api/v1/feeds/rss_feed_items/any" && HasIssuedSession(request) &&
        request.RequestUri?.Query == "?limit=20&media_type=article") return Json(news);
    if (request.Method == HttpMethod.Get && path == "/api/v1/feature-flags")
      return Json("""{"flags":{},"overrides":{}}""");
    if (request.Method == HttpMethod.Get && path == "/api/v1/auth/oauth/providers")
      return Json("""{"providers":[]}""");
    if (request.Method == HttpMethod.Get && path == "/api/v1/localization")
      return new HttpResponseMessage(HttpStatusCode.NotModified);
    return Json("""{"error":"unexpected-auth-news-ui-request"}""", HttpStatusCode.BadRequest);
  }

  // Protected routes require the cookie issued at login, not just the server-side flag.
  private bool HasIssuedSession(HttpRequestMessage request) =>
      authenticated && request.Headers.TryGetValues("Cookie", out var values) &&
      values.SelectMany(value => value.Split(';')).Any(part => part.Trim() == "st=" + sessionToken);

  private static string Jwt()
  {
    static string Segment(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    return Segment("""{"alg":"none"}""") + "." + Segment($$"""{"exp":{{exp}},"jti":"{{Guid.NewGuid()}}"}""") + ".sig";
  }

  private static string? Text(JsonElement body, string key) =>
      body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
  private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
      new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
#endif

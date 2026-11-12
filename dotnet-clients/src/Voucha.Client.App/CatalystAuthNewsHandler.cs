#if DEBUG
using System.Net;
using System.Text;
using System.Text.Json;

namespace Voucha.Client.App;

internal sealed class CatalystAuthNewsHandler : HttpMessageHandler
{
  private readonly string identity = CatalystAuthNewsLaunch.ReadFixture(
      "swift.my.identity.default", "GET", "/api/v1/my/identity");
  // Deliberate unchanged UI replay: producer fixture uses limit=25 and video items
  // within an article feed. This is not an article-filter or response-contract parity test.
  private readonly string news = CatalystAuthNewsLaunch.ReadFixture(
      "web.rss-feed-items.feed.default", "GET", "/api/v1/feeds/rss_feed_items/any");
  private bool requested;
  private bool authenticated;
  private readonly object gate = new();

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
    if (request.Method == HttpMethod.Post && path == "/api/v1/auth/email-address/tokens" &&
        Text(body, "emailAddress") == CatalystAuthNewsLaunch.Email &&
        Text(body, "cfTurnstileResponse") == CatalystAuthNewsLaunch.TurnstileToken)
    {
      requested = true;
      return Json("{}");
    }
    if (request.Method == HttpMethod.Post && path == "/api/v1/auth/email-address/login" && requested &&
        Text(body, "emailAddress") == CatalystAuthNewsLaunch.Email && Text(body, "token") == CatalystAuthNewsLaunch.Code)
    {
      authenticated = true;
      var response = Json("""{"mfa_required":false}""");
      response.Headers.Add("Set-Cookie", new[] { "dt=catalyst-flow-device; Path=/; Secure; HttpOnly", "st=catalyst-flow-session; Path=/; Secure; HttpOnly" });
      return response;
    }
    if (request.Method == HttpMethod.Get && path == "/api/v1/my/identity")
      return authenticated ? Json(identity) : Json("{}", HttpStatusCode.Unauthorized);
    if (request.Method == HttpMethod.Patch && path == "/api/v1/session")
      return Json("""{"session":{"dt":"catalyst-flow-device","st":"catalyst-flow-session","dte":3600,"ste":3600}}""");
    if (request.Method == HttpMethod.Get && path == "/api/v1/feeds/rss_feed_items/any" && authenticated &&
        request.RequestUri?.Query == "?limit=20&media_type=article") return Json(news);
    if (request.Method == HttpMethod.Get && path == "/api/v1/feature-flags")
      return Json("""{"flags":{},"overrides":{}}""");
    if (request.Method == HttpMethod.Get && path == "/api/v1/auth/oauth/providers")
      return Json("""{"providers":[]}""");
    if (request.Method == HttpMethod.Get && path == "/api/v1/localization")
      return new HttpResponseMessage(HttpStatusCode.NotModified);
    return Json("""{"error":"unexpected-auth-news-ui-request"}""", HttpStatusCode.BadRequest);
  }

  private static string? Text(JsonElement body, string key) =>
      body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
  private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
      new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
#endif

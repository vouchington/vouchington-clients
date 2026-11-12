#if DEBUG
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Voucha.Client.App;

internal sealed partial class CatalystGuiFixtureHandler : DelegatingHandler
{
  private const string SessionBody =
      """{"session":{"dt":"catalyst-gui-device","st":"catalyst-gui-session","dte":3600,"ste":3600}}""";
  private const string FeatureFlagsBody = """{"flags":{},"overrides":{}}""";
  private readonly string identityBody;
  private readonly JsonObject preferences;
  private readonly object gate = new();
  private readonly CatalystGuiAudit? audit;

  internal CatalystGuiFixtureHandler(
      HttpMessageHandler inner, string identityBody, JsonObject preferences, CatalystGuiAudit? audit = null)
      : base(inner)
  {
    this.identityBody = identityBody;
    this.preferences = preferences;
    this.audit = audit;
  }


  protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var path = request.RequestUri?.AbsolutePath ?? string.Empty;
    if (request.Method == HttpMethod.Patch && path == "/api/v1/session") return Json(SessionBody);
    if (request.Method == HttpMethod.Get && path == "/api/v1/my/identity") return Json(identityBody);
    if (request.Method == HttpMethod.Get && path == "/api/v1/feature-flags") return Json(FeatureFlagsBody);
    if (request.Method == HttpMethod.Get && path == "/api/v1/localization")
      return new HttpResponseMessage(HttpStatusCode.NotModified);
    if (path == "/api/v1/my/email-preferences" &&
        (request.Method == HttpMethod.Get || request.Method == HttpMethod.Patch))
    {
      if (request.Method == HttpMethod.Patch) await ApplyPatchAsync(request, cancellationToken).ConfigureAwait(false);
      return Json(PreferencesJson());
    }

    return Json("""{"error":"catalyst-gui-fixture"}""", HttpStatusCode.NotFound);
  }

  private async Task ApplyPatchAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    if (request.Content is null) return;
    var patchText = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    JsonObject? patch;
    try
    {
      patch = JsonNode.Parse(string.IsNullOrWhiteSpace(patchText) ? "{}" : patchText) as JsonObject;
    }
    catch (JsonException)
    {
      return;
    }

    if (patch is null) return;
    lock (gate)
    {
      if (preferences["email_preferences"] is not JsonObject current) return;
      var after = current.DeepClone().AsObject();
      foreach (var property in patch) after[property.Key] = property.Value?.DeepClone();
      audit?.Patch(patch, current, after);
      preferences["email_preferences"] = after;
    }
  }

  private string PreferencesJson()
  {
    lock (gate) return preferences.ToJsonString();
  }

  private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
      new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
#endif

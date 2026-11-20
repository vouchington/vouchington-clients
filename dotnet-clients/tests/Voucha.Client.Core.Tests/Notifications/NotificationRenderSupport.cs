using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Notifications;

namespace Voucha.Client.Core.Tests.Notifications;

/// <summary>Renders one notification built from JSON fragments, the way the wire shape arrives.</summary>
internal static class NotificationRenderSupport
{
  public static readonly Uri WebBase = new("https://voucha.example");

  public static NotificationsResponse Response(string kind, string notification, string hydration)
  {
    var kindJson = JsonSerializer.Serialize(kind);
    var body = string.Join(",", new[] { $"\"id\":\"n\",\"kind\":{kindJson}", notification }
        .Where(part => part.Length > 0));
    var json = "{\"results\":[],\"page_info\":{\"has_next_page\":false},"
        + $"\"notifications\":{{\"n\":{{{body}}}}}"
        + (hydration.Length > 0 ? "," + hydration : "") + "}";
    return JsonSerializer.Deserialize<NotificationsResponse>(json, VouchaApiJson.Options)!;
  }

  public static (string Title, string Body) Render(
      string kind,
      string notification = "",
      string hydration = "",
      string locale = "en",
      Uri? webBase = null)
  {
    var response = Response(kind, notification, hydration);
    var text = NotificationRenderer.Render(response.Notifications["n"], response, webBase ?? WebBase);
    var row = new NotificationRow("n", text, null, null, null, false)
        .WithLocalization(new UiLocalization(new UiLocaleController(new StubDeviceLanguageProvider(locale))));
    return (row.LocalizedTitle, row.LocalizedBody);
  }
}

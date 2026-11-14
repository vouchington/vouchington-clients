namespace Voucha.Client.Core;

public static class SentryTelemetryFields
{
  public static string? ScrubAddress(string? value) =>
      SentryEventScrubber.Scrub(new SentryOutboundEvent(
          null,
          null,
          null,
          null,
          value,
          null,
          null,
          null,
          null,
          null,
          null)).RequestTarget;

  public static string? ScrubField(string key, string value)
  {
    ArgumentNullException.ThrowIfNull(key);
    ArgumentNullException.ThrowIfNull(value);
    var breadcrumb = SentryEventScrubber.ScrubBreadcrumb(new SentryBreadcrumbDraft(
        "http",
        null,
        new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value }));
    return breadcrumb?.Data is not null && breadcrumb.Data.TryGetValue(key, out var kept) ? kept : null;
  }

  public static bool KeepsNumber(string key)
  {
    ArgumentNullException.ThrowIfNull(key);
    var name = key.Trim();
    return name.Equals("status_code", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("request_body_size", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("response_body_size", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("http.response.status_code", StringComparison.OrdinalIgnoreCase);
  }
}

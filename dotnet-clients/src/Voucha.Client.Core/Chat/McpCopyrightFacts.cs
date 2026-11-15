using System.Text.Json;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed record McpCopyrightNotice(string? Claimant, string Jurisdiction, DateTimeOffset ReceivedAt, string? WorkDescription);

/// <summary>Public case labels only. Notice ids and correspondence never reach the fact.</summary>
public sealed record McpCopyrightNoticesFact(IReadOnlyList<McpCopyrightNotice> Notices) : McpResultFact
{
  internal override IReadOnlyList<string> Lines(IUiLocalization localization)
  {
    var lines = new List<string> { localization.Localize(UiMessageKey.NativeMcpFactsCopyrightNoticesSearchTitle) };
    if (Notices.Count == 0) lines.Add(localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceNoResults));
    foreach (var notice in Notices)
    {
      if (notice.Claimant is not null)
        lines.Add($"{localization.Localize(UiMessageKey.NativeCopyrightNoticesClaimant)}: " +
            localization.Resolve(UiText.UserContent(notice.Claimant)));
      lines.Add($"{localization.Localize(UiMessageKey.NativeMcpFactsCopyrightNoticesJurisdiction)}: " +
          (notice.Jurisdiction == "eu_dsa" ? "DSA" : "DMCA"));
      lines.Add(localization.Format(UiMessageKey.NativeCopyrightNoticesReceivedDate,
          ("date", localization.FormatDateTime(notice.ReceivedAt, TimeZoneInfo.Local))));
      if (notice.WorkDescription is not null)
        lines.Add($"{localization.Localize(UiMessageKey.NativeMcpFactsCopyrightNoticesWorkDescription)}: " +
            localization.Resolve(UiText.UserContent(notice.WorkDescription)));
    }

    return lines;
  }
}

internal static class McpCopyrightFacts
{
  private const int MaximumNotices = 100;

  internal static McpResultFact? Notices(JsonElement content)
  {
    if (!McpFactJson.Array(content, "copyright_notices", out var rows) || rows.GetArrayLength() > MaximumNotices)
      return null;
    var ids = new HashSet<string>(StringComparer.Ordinal);
    var notices = new List<McpCopyrightNotice>();
    foreach (var row in rows.EnumerateArray())
    {
      if (!McpFactJson.Text(row, "id", out var id) || !Guid.TryParse(id, out _) || !ids.Add(id) ||
          !McpFactJson.OptionalText(row, "claimant_display_name", out var claimant) ||
          !McpFactJson.OptionalText(row, "work_description", out var work) ||
          !McpFactJson.Text(row, "jurisdiction", out var jurisdiction) ||
          jurisdiction is not ("us_dmca" or "eu_dsa") ||
          !McpFactJson.Text(row, "received_at", out var received) ||
          !DateTimeOffset.TryParse(received, System.Globalization.CultureInfo.InvariantCulture,
              System.Globalization.DateTimeStyles.AssumeUniversal, out var receivedAt)) return null;
      notices.Add(new McpCopyrightNotice(
          McpFactJson.Displayable(claimant) ? claimant : null, jurisdiction, receivedAt,
          McpFactJson.Displayable(work) ? work : null));
    }

    return new McpCopyrightNoticesFact(notices);
  }
}

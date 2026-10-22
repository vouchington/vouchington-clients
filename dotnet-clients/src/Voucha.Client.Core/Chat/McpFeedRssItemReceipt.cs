using System.Text.Json;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

/// <summary>A validated RSS delivery reference. It is not a hydrated feed item.</summary>
public sealed record McpFeedRssItemReceipt(string AccountId, string CallId, IReadOnlyList<string> ItemIds)
{
  internal static McpFeedRssItemReceipt? FromValidatedResult(
      string tool, JsonElement arguments, JsonElement structuredContent,
      string accountId, string callId)
  {
    if (tool != "read_feed" || string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(callId) ||
        arguments.ValueKind != JsonValueKind.Object ||
        !arguments.TryGetProperty("option", out var option) || option.GetString() != "rss_items" ||
        structuredContent.ValueKind != JsonValueKind.Object ||
        !structuredContent.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
        !structuredContent.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array ||
        results.GetArrayLength() > 100) return null;
    var ids = new List<string>();
    foreach (var row in results.EnumerateArray())
    {
      if (row.ValueKind != JsonValueKind.Object ||
          !row.TryGetProperty("entity_id", out var id) || id.ValueKind != JsonValueKind.String ||
          id.GetString() is not { } value || !Guid.TryParse(value, out _)) return null;
      if (ids.Contains(value, StringComparer.Ordinal)) return null;
      ids.Add(value);
    }
    return new McpFeedRssItemReceipt(accountId, callId, ids);
  }

  internal static string? Display(IReadOnlyList<McpFeedRssItemReceipt>? receipts, IUiLocalization? localization)
  {
    if (receipts is not { Count: > 0 }) return null;
    var culture = localization ?? UiLocalization.English;
    var lines = new List<string> { culture.Localize(UiMessageKey.NativeTaxonomyListsRssFeedItem) };
    foreach (var receipt in receipts)
    {
      if (receipt.ItemIds.Count == 0)
      {
        lines.Add(culture.Localize(UiMessageKey.NativeSwiftRouteSurfaceNoResults));
        continue;
      }
      foreach (var id in receipt.ItemIds)
        lines.Add(culture.Format(
            UiMessageKey.NativeSwiftIntegrityRssItemId, ("id", UiText.ProtocolValue(id))));
    }
    return string.Join("\n", lines);
  }
}

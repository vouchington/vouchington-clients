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
    if (McpFeedReceiptReader.ReadEntityIds(
            tool, arguments, structuredContent, accountId, callId, "rss_items") is not { } ids)
      return null;
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

using System.Text.Json;

namespace Voucha.Client.Core.Chat;

internal static class McpFeedReceiptReader
{
  internal static JsonElement? ReadResults(
      string tool,
      JsonElement arguments,
      JsonElement structuredContent,
      string accountId,
      string callId,
      string option)
  {
    if (tool != "read_feed" || string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(callId) ||
        arguments.ValueKind != JsonValueKind.Object ||
        !arguments.TryGetProperty("option", out var selected) || selected.GetString() != option ||
        structuredContent.ValueKind != JsonValueKind.Object ||
        !structuredContent.TryGetProperty("success", out var success) ||
        success.ValueKind != JsonValueKind.True ||
        !structuredContent.TryGetProperty("results", out var results) ||
        results.ValueKind != JsonValueKind.Array ||
        results.GetArrayLength() > 100) return null;
    return results;
  }

  internal static IReadOnlyList<string>? ReadEntityIds(
      string tool,
      JsonElement arguments,
      JsonElement structuredContent,
      string accountId,
      string callId,
      string option)
  {
    if (ReadResults(tool, arguments, structuredContent, accountId, callId, option) is not { } results)
      return null;
    var ids = new List<string>();
    foreach (var row in results.EnumerateArray())
    {
      if (row.ValueKind != JsonValueKind.Object ||
          !row.TryGetProperty("entity_id", out var id) || id.ValueKind != JsonValueKind.String ||
          id.GetString() is not { } value || !Guid.TryParse(value, out _)) return null;
      if (ids.Contains(value, StringComparer.Ordinal)) return null;
      ids.Add(value);
    }

    return ids;
  }
}

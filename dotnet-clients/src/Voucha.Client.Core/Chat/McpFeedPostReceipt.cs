using System.Text.Json;

namespace Voucha.Client.Core.Chat;

/// <summary>A validated delivery reference. It is not a hydrated post.</summary>
public sealed record McpFeedPostReceipt(string AccountId, string CallId, IReadOnlyList<string> PostIds)
{
  internal static McpFeedPostReceipt? FromValidatedResult(
      string tool, JsonElement arguments, JsonElement structuredContent,
      string accountId, string callId)
  {
    if (tool != "read_feed" || string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(callId) ||
        arguments.ValueKind != JsonValueKind.Object ||
        !arguments.TryGetProperty("option", out var option) || option.GetString() != "posts" ||
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
    return new McpFeedPostReceipt(accountId, callId, ids);
  }
}

using System.Text.Json;

namespace Voucha.Client.Core.Chat;

/// <summary>A validated delivery reference. It is not a hydrated post.</summary>
public sealed record McpFeedPostReceipt(string AccountId, string CallId, IReadOnlyList<string> PostIds)
{
  internal static McpFeedPostReceipt? FromValidatedResult(
      string tool, JsonElement arguments, JsonElement structuredContent,
      string accountId, string callId)
  {
    if (McpFeedReceiptReader.ReadEntityIds(
            tool, arguments, structuredContent, accountId, callId, "posts") is not { } ids)
      return null;
    return new McpFeedPostReceipt(accountId, callId, ids);
  }
}

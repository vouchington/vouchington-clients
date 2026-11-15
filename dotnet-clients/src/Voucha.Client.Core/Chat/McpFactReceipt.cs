using System.Text.Json;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

/// <summary>A typed fact read from a validated MCP result. It never carries raw tool output.</summary>
public abstract record McpResultFact
{
  internal abstract IReadOnlyList<string> Lines(IUiLocalization localization);
}

/// <summary>A typed fact owned by the trusted account and provider call that produced it.</summary>
public sealed record McpFactReceipt(string AccountId, string CallId, McpResultFact Fact)
{
  internal static McpFactReceipt? FromValidatedResult(
      string tool, JsonElement arguments, JsonElement structuredContent, string accountId, string callId)
  {
    if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(callId) ||
        arguments.ValueKind != JsonValueKind.Object ||
        structuredContent.ValueKind != JsonValueKind.Object ||
        !structuredContent.TryGetProperty("success", out var success) ||
        success.ValueKind != JsonValueKind.True) return null;
    var option = arguments.TryGetProperty("option", out var selected) &&
        selected.ValueKind == JsonValueKind.String ? selected.GetString() : null;
    McpResultFact? fact = (tool, option) switch
    {
      ("read_my_preferences", "email") => McpPreferenceFacts.Email(structuredContent),
      ("read_my_preferences", "general") => McpPreferenceFacts.General(structuredContent),
      ("read_my_profile", "bio") => McpProfileFacts.Bio(structuredContent),
      ("read_my_profile", "links") => McpProfileFacts.Links(structuredContent),
      ("read_posts", "search") => McpContentFacts.PostSearch(structuredContent),
      ("read_posts", "details") => McpContentFacts.PostDetails(structuredContent),
      ("read_rss_feed_item", "list") => McpContentFacts.RssList(structuredContent),
      ("read_rss_feed_item", "details") => McpContentFacts.RssDetails(structuredContent),
      ("get_story", null) => McpContentFacts.Story(structuredContent),
      _ => null,
    };
    return fact is null ? null : new McpFactReceipt(accountId, callId, fact);
  }

  internal static string? Display(IReadOnlyList<McpFactReceipt>? receipts, IUiLocalization? localization)
  {
    if (receipts is not { Count: > 0 }) return null;
    var culture = localization ?? UiLocalization.English;
    return string.Join("\n\n", receipts.Select(receipt => string.Join("\n", receipt.Fact.Lines(culture))));
  }

  internal bool IsOwnedBy(string accountId, string callId) =>
      AccountId == accountId && CallId == callId && !string.IsNullOrEmpty(callId);
}

using System.Text.Json;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

/// <summary>Predicate and entity types only. Entity identifiers and bookmark rows never reach the fact.</summary>
public sealed record McpBookmarkFact(bool Removed, string Predicate, IReadOnlyList<string> EntityTypes) : McpResultFact
{
  internal override IReadOnlyList<string> Lines(IUiLocalization localization) =>
  [
    localization.Localize(Removed ? UiMessageKey.NativeMcpFactsBookmarkRemoved : UiMessageKey.NativeMcpFactsBookmarkSet),
    string.Join(" · ", EntityTypes.Select(McpBookmarkFacts.EntityTypeKey).Select(localization.Localize)
        .Prepend(localization.Localize(McpBookmarkFacts.PredicateKey(Predicate)!.Value))),
  ];
}

internal static class McpBookmarkFacts
{
  internal static UiMessageKey? PredicateKey(string predicate) => predicate switch
  {
    "save" => UiMessageKey.NativeMcpFactsBookmarkPredicateSave,
    "follow" => UiMessageKey.NativeMcpFactsBookmarkPredicateFollow,
    "mute" => UiMessageKey.NativeMcpFactsBookmarkPredicateMute,
    "block" => UiMessageKey.NativeMcpFactsBookmarkPredicateBlock,
    _ => null,
  };

  internal static UiMessageKey EntityTypeKey(string type) => TypeKey(type)!.Value;

  private static UiMessageKey? TypeKey(string type) => type switch
  {
    "post" => UiMessageKey.NativeMcpFactsBookmarkEntityTypePost,
    "topic" => UiMessageKey.NativeMcpFactsBookmarkEntityTypeTopic,
    "user" => UiMessageKey.NativeMcpFactsBookmarkEntityTypeUser,
    "community" => UiMessageKey.NativeMcpFactsBookmarkEntityTypeCommunity,
    "url" => UiMessageKey.NativeMcpFactsBookmarkEntityTypeUrl,
    "rss_feed" => UiMessageKey.NativeMcpFactsBookmarkEntityTypeRssFeed,
    "rss_feed_item" => UiMessageKey.NativeMcpFactsBookmarkEntityTypeRssFeedItem,
    "url_hostname" => UiMessageKey.NativeMcpFactsBookmarkEntityTypeUrlHostname,
    _ => null,
  };

  /// <summary>Set: the call's predicate and the entity types of results the server reports as set.</summary>
  internal static McpResultFact? Set(JsonElement arguments, JsonElement content)
  {
    if (!McpFactJson.Text(Nested(arguments), "predicate", out var predicate) || PredicateKey(predicate) is null ||
        !content.TryGetProperty("results", out var rows) || rows.ValueKind != JsonValueKind.Array) return null;
    var types = new List<string>();
    foreach (var row in rows.EnumerateArray())
    {
      if (!McpFactJson.Text(row, "status", out var status) || status != "set") continue;
      if (!McpFactJson.Text(row, "entity_type", out var type) || TypeKey(type) is null) return null;
      if (!types.Contains(type)) types.Add(type);
    }

    return types.Count == 0 ? null : new McpBookmarkFact(false, predicate, types);
  }

  /// <summary>Remove: the call's own predicate and entity type; the result carries only success.</summary>
  internal static McpResultFact? Remove(JsonElement arguments, JsonElement content)
  {
    var call = Nested(arguments);
    return !content.TryGetProperty("results", out _) &&
        McpFactJson.Text(call, "predicate", out var predicate) && PredicateKey(predicate) is not null &&
        McpFactJson.Text(call, "entity_type", out var type) && TypeKey(type) is not null
            ? new McpBookmarkFact(true, predicate, [type]) : null;
  }

  private static JsonElement Nested(JsonElement arguments) =>
      arguments.TryGetProperty("arguments", out var nested) ? nested : default;
}

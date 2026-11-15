using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed record McpContentItem(string Title, string? FeedTitle, HackerNewsReferences? References);

/// <summary>
/// Public titles and stored Hacker News counts for posts, RSS items or a story's items.
/// Authorship, markdown, URLs and identifiers never reach the fact.
/// </summary>
public sealed record McpContentFact(bool IsPosts, IReadOnlyList<McpContentItem> Items) : McpResultFact
{
  internal override IReadOnlyList<string> Lines(IUiLocalization localization)
  {
    var lines = new List<string>
    {
      localization.Localize(IsPosts ? UiMessageKey.NativeDotnetResidualPosts : UiMessageKey.NativeTaxonomyListsRssFeedItem),
    };
    if (Items.Count == 0)
      lines.Add(localization.Localize(IsPosts
          ? UiMessageKey.NativeDotnetPostsNoPostsYet : UiMessageKey.NativeSwiftRouteSurfaceNoResults));
    foreach (var item in Items)
    {
      lines.Add(localization.Resolve(UiText.UserContent(item.Title)));
      if (item.FeedTitle is not null) lines.Add(localization.Resolve(UiText.UserContent(item.FeedTitle)));
      AddReferences(lines, item.References, localization);
    }

    return lines;
  }

  private static void AddReferences(
      List<string> lines, HackerNewsReferences? references, IUiLocalization localization)
  {
    var hackerNews = new HackerNewsPresentation(references, localization);
    if (!hackerNews.HasReferences) return;
    lines.Add(hackerNews.Summary);
    if (hackerNews.Stories.Count > 0)
      lines.AddRange(hackerNews.Stories.Select(story => story.Label).Prepend(hackerNews.SeenHeading));
    if (hackerNews.Comments.Count > 0)
      lines.AddRange(hackerNews.Comments.Select(comment => comment.Label).Prepend(hackerNews.MentionedHeading));
  }
}

internal static class McpContentFacts
{
  private const int MaximumItems = 100;

  internal static McpResultFact? PostSearch(JsonElement content) =>
      McpFactJson.Array(content, "results", out var rows) ? Items(rows, posts: true) : null;

  internal static McpResultFact? PostDetails(JsonElement content) =>
      McpFactJson.Object(content, "post", out var post) ? Single(post, posts: true) : null;

  internal static McpResultFact? RssList(JsonElement content) =>
      McpFactJson.Array(content, "results", out var rows) ? Items(rows, posts: false) : null;

  internal static McpResultFact? RssDetails(JsonElement content) =>
      McpFactJson.Object(content, "rss_feed_item", out var item) ? Single(item, posts: false) : null;

  internal static McpResultFact? Story(JsonElement content) =>
      McpFactJson.Array(content, "items", out var rows) ? Items(rows, posts: false) : null;

  private static McpContentFact? Single(JsonElement row, bool posts)
  {
    using var rows = JsonDocument.Parse($"[{row.GetRawText()}]");
    return Items(rows.RootElement, posts);
  }

  private static McpContentFact? Items(JsonElement rows, bool posts)
  {
    if (rows.GetArrayLength() > MaximumItems) return null;
    var ids = new HashSet<string>(StringComparer.Ordinal);
    var items = new List<McpContentItem>();
    foreach (var row in rows.EnumerateArray())
    {
      if (!McpFactJson.Text(row, "id", out var id) || !Guid.TryParse(id, out _) || !ids.Add(id) ||
          !McpFactJson.Text(row, "title", out var title) || !McpFactJson.Displayable(title) ||
          !References(row, out var references)) return null;
      string? feedTitle = null;
      if (!posts && (!McpFactJson.Text(row, "rss_feed_title", out var feed) ||
          !McpFactJson.Displayable(feedTitle = feed))) return null;
      items.Add(new McpContentItem(title, feedTitle, references));
    }

    return new McpContentFact(posts, items);
  }

  private static bool References(JsonElement row, out HackerNewsReferences? references)
  {
    references = null;
    if (!row.TryGetProperty("hacker_news_references", out var source) ||
        source.ValueKind == JsonValueKind.Null) return true;
    try { references = source.Deserialize<HackerNewsReferences>(); }
    catch (JsonException) { return false; }
    return references is { StoryCount: >= 0, CommentCount: >= 0 } &&
        (references.Stories?.Count ?? 0) <= MaximumItems && (references.Comments?.Count ?? 0) <= MaximumItems;
  }
}

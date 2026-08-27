using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum BookmarkPredicate
{
  Save,
  Hide,
  Follow,
  Mute,
  Block,
  Subscribe,
  DismissRecommendation,
  ProxyFollow,
  ProxyMute,
}

public static class BookmarkPredicateExtensions
{
  public static string ToApiName(this BookmarkPredicate predicate) =>
      predicate switch
      {
        BookmarkPredicate.Save => "save",
        BookmarkPredicate.Hide => "hide",
        BookmarkPredicate.Follow => "follow",
        BookmarkPredicate.Mute => "mute",
        BookmarkPredicate.Block => "block",
        BookmarkPredicate.Subscribe => "subscribe",
        BookmarkPredicate.DismissRecommendation => "dismiss_recommendation",
        BookmarkPredicate.ProxyFollow => "proxy_follow",
        BookmarkPredicate.ProxyMute => "proxy_mute",
        _ => throw new ArgumentOutOfRangeException(nameof(predicate), predicate, null),
      };
}

public sealed record BookmarkPredicates(
    [property: JsonPropertyName("save")] bool? Save = null,
    [property: JsonPropertyName("hide")] bool? Hide = null,
    [property: JsonPropertyName("follow")] bool? Follow = null,
    [property: JsonPropertyName("mute")] bool? Mute = null,
    [property: JsonPropertyName("block")] bool? Block = null,
    [property: JsonPropertyName("subscribe")] bool? Subscribe = null,
    [property: JsonPropertyName("dismiss_recommendation")] bool? DismissRecommendation = null,
    [property: JsonPropertyName("proxy_follow")] bool? ProxyFollow = null,
    [property: JsonPropertyName("proxy_mute")] bool? ProxyMute = null)
{
  public bool IsActive(BookmarkPredicate predicate) =>
      predicate switch
      {
        BookmarkPredicate.Save => Save == true,
        BookmarkPredicate.Hide => Hide == true,
        BookmarkPredicate.Follow => Follow == true,
        BookmarkPredicate.Mute => Mute == true,
        BookmarkPredicate.Block => Block == true,
        BookmarkPredicate.Subscribe => Subscribe == true,
        BookmarkPredicate.DismissRecommendation => DismissRecommendation == true,
        BookmarkPredicate.ProxyFollow => ProxyFollow == true,
        BookmarkPredicate.ProxyMute => ProxyMute == true,
        _ => throw new ArgumentOutOfRangeException(nameof(predicate), predicate, null),
      };
}

public static class BookmarkSidecar
{
  public static bool IsActive(
      IReadOnlyDictionary<string, BookmarkPredicates>? bookmarks,
      string? entityId,
      BookmarkPredicate predicate) =>
      bookmarks is not null &&
      entityId is not null &&
      bookmarks.TryGetValue(entityId, out var predicates) &&
      predicates.IsActive(predicate);
}

public sealed record EntityBookmarksResponse(
    [property: JsonPropertyName("bookmarks")] BookmarkPredicates Bookmarks);

#pragma warning disable CA1054, CA1056, CA1720

public sealed record BookmarkCollectionReference(
    [property: JsonPropertyName("id")] string Id);

public sealed record BookmarkCollectionResponse<T>(
    [property: JsonPropertyName("results")] IReadOnlyList<T> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("rss_feed_item_thumbnail_url")] IReadOnlyDictionary<string, string>? RssFeedItemThumbnailUrl = null,
    [property: JsonPropertyName("topic_elections")] IReadOnlyDictionary<string, TopicElection>? TopicElections = null,
    [property: JsonPropertyName("hostname_elections")] IReadOnlyDictionary<string, HostnameElection>? HostnameElections = null);

public sealed record PublisherTypeTopic(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("slug")] string Slug);

public sealed record PublisherTypeTopicsResponse(
    [property: JsonPropertyName("publisher_types")] IReadOnlyList<PublisherTypeTopic> PublisherTypes);

public sealed record UserTagTopicsResponse(
    [property: JsonPropertyName("user_tags")] IReadOnlyList<PublisherTypeTopic> UserTags);

#pragma warning restore CA1054, CA1056, CA1720

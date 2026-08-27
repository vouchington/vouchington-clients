using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Bookmarks;

public static class BookmarkCollectionActions
{
  public static BookmarkInverseAction? For(BookmarkCollectionKind kind, string listType)
  {
    var entityType = kind switch
    {
      BookmarkCollectionKind.Posts => "post",
      BookmarkCollectionKind.Topics => "topic",
      BookmarkCollectionKind.Users => "user",
      BookmarkCollectionKind.RssFeedItems => "rss_feed_item",
      BookmarkCollectionKind.RssFeeds => "rss_feed",
      BookmarkCollectionKind.Urls => "url",
      BookmarkCollectionKind.Hostnames => "url_hostname",
      BookmarkCollectionKind.Communities => "community",
      _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
    return listType switch
    {
      "saved" => new(entityType, BookmarkPredicate.Save, UiMessageKey.NativeSwiftHouseholdsBookmarksUnsave),
      "hidden" => new(entityType, BookmarkPredicate.Hide, UiMessageKey.NativeSwiftHouseholdsBookmarksUnhide),
      "following" => new(entityType, BookmarkPredicate.Follow, UiMessageKey.NativeSwiftHouseholdsBookmarksUnfollow),
      "subscribed" or "subscribed-posts" => new(
          entityType,
          BookmarkPredicate.Subscribe,
          UiMessageKey.NativeSwiftHouseholdsBookmarksUnsubscribe),
      "muted" => new(entityType, BookmarkPredicate.Mute, UiMessageKey.NativeSwiftHouseholdsBookmarksUnmute),
      "blocked" => new(entityType, BookmarkPredicate.Block, UiMessageKey.NativeSwiftHouseholdsBookmarksUnblock),
      "dismissed-recommendations" => new(
          entityType,
          BookmarkPredicate.DismissRecommendation,
          UiMessageKey.NativeSwiftHouseholdsBookmarksRestore),
      "proxy-following" => new(
          entityType,
          BookmarkPredicate.ProxyFollow,
          UiMessageKey.NativeSwiftHouseholdsBookmarksRemoveProxyFollow),
      "proxy-muted" => new(
          entityType,
          BookmarkPredicate.ProxyMute,
          UiMessageKey.NativeSwiftHouseholdsBookmarksRemoveProxyMute),
      _ => null,
    };
  }
}

using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Bookmarks;

public static class BookmarkCollectionRoutes
{
  private static readonly Dictionary<string, BookmarkCollectionRouteContext> Routes = new(StringComparer.Ordinal)
  {
    ["/my/posts/saved"] = new("/my/posts/saved", UiMessageKey.NativeDotnetBookmarksSavedPosts, BookmarkCollectionKind.Posts, "saved"),
    ["/my/posts/hidden"] = new("/my/posts/hidden", UiMessageKey.NativeDotnetBookmarksHiddenPosts, BookmarkCollectionKind.Posts, "hidden"),
    ["/my/posts/following"] = new("/my/posts/following", UiMessageKey.NativeDotnetBookmarksFollowedPosts, BookmarkCollectionKind.Posts, "following"),
    ["/my/posts/subscribed"] = new("/my/posts/subscribed", UiMessageKey.NativeDotnetBookmarksSubscribedPosts, BookmarkCollectionKind.Posts, "subscribed"),
    ["/my/news-items/saved"] = new("/my/news-items/saved", UiMessageKey.NativeDotnetBookmarksSavedNewsItems, BookmarkCollectionKind.RssFeedItems, "saved", MediaType: "article"),
    ["/my/news-items/hidden"] = new("/my/news-items/hidden", UiMessageKey.NativeDotnetBookmarksHiddenNewsItems, BookmarkCollectionKind.RssFeedItems, "hidden", MediaType: "article"),
    ["/my/news-items/viewed"] = new("/my/news-items/viewed", UiMessageKey.NativeDotnetBookmarksViewedNewsItems, BookmarkCollectionKind.RssFeedItems, "viewed", MediaType: "article"),
    ["/my/rss-feed-items/saved"] = new("/my/rss-feed-items/saved", UiMessageKey.NativeDotnetBookmarksSavedNewsItems, BookmarkCollectionKind.RssFeedItems, "saved", MediaType: "article"),
    ["/my/rss-feed-items/hidden"] = new("/my/rss-feed-items/hidden", UiMessageKey.NativeDotnetBookmarksHiddenNewsItems, BookmarkCollectionKind.RssFeedItems, "hidden", MediaType: "article"),
    ["/my/rss-feed-items/viewed"] = new("/my/rss-feed-items/viewed", UiMessageKey.NativeDotnetBookmarksViewedNewsItems, BookmarkCollectionKind.RssFeedItems, "viewed", MediaType: "article"),
    ["/my/news-sources"] = new("/my/news-sources", UiMessageKey.NativeDotnetBookmarksMyNewsSources, BookmarkCollectionKind.RssFeeds, "following", FeedType: "article"),
    ["/my/news-sources/muted"] = new("/my/news-sources/muted", UiMessageKey.NativeDotnetBookmarksMutedNewsSources, BookmarkCollectionKind.RssFeeds, "muted", FeedType: "article"),
    ["/my/news-sources/viewed"] = new("/my/news-sources/viewed", UiMessageKey.NativeDotnetBookmarksViewedNewsSources, BookmarkCollectionKind.RssFeeds, "viewed", FeedType: "article"),
    ["/my/podcast-episodes/saved"] = new("/my/podcast-episodes/saved", UiMessageKey.NativeDotnetBookmarksSavedPodcastEpisodes, BookmarkCollectionKind.RssFeedItems, "saved", MediaType: "audio"),
    ["/my/podcast-episodes/hidden"] = new("/my/podcast-episodes/hidden", UiMessageKey.NativeDotnetBookmarksHiddenPodcastEpisodes, BookmarkCollectionKind.RssFeedItems, "hidden", MediaType: "audio"),
    ["/my/podcast-episodes/viewed"] = new("/my/podcast-episodes/viewed", UiMessageKey.NativeDotnetBookmarksViewedPodcastEpisodes, BookmarkCollectionKind.RssFeedItems, "viewed", MediaType: "audio"),
    ["/my/podcasts"] = new("/my/podcasts", UiMessageKey.NativeDotnetBookmarksMyPodcasts, BookmarkCollectionKind.RssFeeds, "following", FeedType: "podcast"),
    ["/my/topics/following"] = new("/my/topics/following", UiMessageKey.NativeDotnetBookmarksFollowedTopics, BookmarkCollectionKind.Topics, "following"),
    ["/my/topics/muted"] = new("/my/topics/muted", UiMessageKey.NativeDotnetBookmarksMutedTopics, BookmarkCollectionKind.Topics, "muted"),
    ["/my/topics/blocked"] = new("/my/topics/blocked", UiMessageKey.NativeDotnetBookmarksBlockedTopics, BookmarkCollectionKind.Topics, "blocked"),
    ["/my/topics/viewed"] = new("/my/topics/viewed", UiMessageKey.NativeDotnetBookmarksViewedTopics, BookmarkCollectionKind.Topics, "viewed"),
    ["/my/topics/dismissed-recommendations"] = new(
        "/my/topics/dismissed-recommendations",
        UiMessageKey.NativeDotnetBookmarksDismissedTopicRecommendations,
        BookmarkCollectionKind.Topics,
        "dismissed-recommendations"),
    ["/my/friend-recommendations/dismissed"] = new(
        "/my/friend-recommendations/dismissed",
        UiMessageKey.NativeDotnetBookmarksDismissedFriendRecommendations,
        BookmarkCollectionKind.Users,
        "dismissed-recommendations"),
    ["/my/users/dismissed-recommendations"] = new(
        "/my/users/dismissed-recommendations",
        UiMessageKey.NativeDotnetBookmarksDismissedFriendRecommendations,
        BookmarkCollectionKind.Users,
        "dismissed-recommendations"),
    ["/my/users/following"] = new("/my/users/following", UiMessageKey.NativeDotnetBookmarksFollowedUsers, BookmarkCollectionKind.Users, "following"),
    ["/my/users/followers"] = new("/my/users/followers", UiMessageKey.NativeDotnetBookmarksFollowers, BookmarkCollectionKind.Users, "followers"),
    ["/my/users/subscribed-posts"] = new("/my/users/subscribed-posts", UiMessageKey.NativeDotnetBookmarksSubscribedToPosts, BookmarkCollectionKind.Users, "subscribed-posts"),
    ["/my/users/muted"] = new("/my/users/muted", UiMessageKey.NativeDotnetBookmarksMutedUsers, BookmarkCollectionKind.Users, "muted"),
    ["/my/users/blocked"] = new("/my/users/blocked", UiMessageKey.NativeDotnetBookmarksBlockedUsers, BookmarkCollectionKind.Users, "blocked"),
    ["/my/podcasts/muted"] = new("/my/podcasts/muted", UiMessageKey.NativeDotnetBookmarksMutedPodcasts, BookmarkCollectionKind.RssFeeds, "muted", FeedType: "podcast"),
    ["/my/podcasts/viewed"] = new("/my/podcasts/viewed", UiMessageKey.NativeDotnetBookmarksViewedPodcasts, BookmarkCollectionKind.RssFeeds, "viewed", FeedType: "podcast"),
    ["/my/videos/saved"] = new("/my/videos/saved", UiMessageKey.NativeDotnetBookmarksSavedVideos, BookmarkCollectionKind.RssFeedItems, "saved", MediaType: "video"),
    ["/my/videos/hidden"] = new("/my/videos/hidden", UiMessageKey.NativeDotnetBookmarksHiddenVideos, BookmarkCollectionKind.RssFeedItems, "hidden", MediaType: "video"),
    ["/my/videos/viewed"] = new("/my/videos/viewed", UiMessageKey.NativeDotnetBookmarksViewedVideos, BookmarkCollectionKind.RssFeedItems, "viewed", MediaType: "video"),
    ["/my/channels"] = new("/my/channels", UiMessageKey.NativeDotnetBookmarksMyChannels, BookmarkCollectionKind.RssFeeds, "following", FeedType: "video"),
    ["/my/channels/muted"] = new("/my/channels/muted", UiMessageKey.NativeDotnetBookmarksMutedChannels, BookmarkCollectionKind.RssFeeds, "muted", FeedType: "video"),
    ["/my/channels/viewed"] = new("/my/channels/viewed", UiMessageKey.NativeDotnetBookmarksViewedChannels, BookmarkCollectionKind.RssFeeds, "viewed", FeedType: "video"),
    ["/my/urls/saved"] = new("/my/urls/saved", UiMessageKey.NativeDotnetBookmarksSavedLinks, BookmarkCollectionKind.Urls, "saved"),
    ["/my/domains/muted"] = new("/my/domains/muted", UiMessageKey.NativeDotnetBookmarksMutedDomains, BookmarkCollectionKind.Hostnames, "muted"),
    ["/my/domains/blocked"] = new("/my/domains/blocked", UiMessageKey.NativeDotnetBookmarksBlockedDomains, BookmarkCollectionKind.Hostnames, "blocked"),
    ["/my/communities/saved"] = new("/my/communities/saved", UiMessageKey.NativeDotnetBookmarksSavedCommunities, BookmarkCollectionKind.Communities, "saved"),
    ["/my/communities/proxy-following"] = new("/my/communities/proxy-following", UiMessageKey.NativeDotnetBookmarksProxyFollowedCommunities, BookmarkCollectionKind.Communities, "proxy-following"),
    ["/my/communities/proxy-muted"] = new("/my/communities/proxy-muted", UiMessageKey.NativeDotnetBookmarksProxyMutedCommunities, BookmarkCollectionKind.Communities, "proxy-muted"),
  };

  public static bool TryResolve(string path, out BookmarkCollectionRouteContext context)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);
    return Routes.TryGetValue(path, out context!);
  }
}

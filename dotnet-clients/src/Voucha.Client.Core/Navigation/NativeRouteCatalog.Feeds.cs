namespace Voucha.Client.Core.Navigation;

public static partial class NativeRouteCatalog
{
  private static NativeRouteCatalogEntry[] FeedEntries() =>
  [
    Included(
        "Feed posts",
        NativeRouteDestinationId.FeedPosts,
        "/feed/posts",
        ["/feed", "/feed/posts", "/feed/posts/friends", "/feed/posts/topics"]),
    Included(
        "Feed news",
        NativeRouteDestinationId.FeedNews,
        "/feed/news",
        ["/feed/news", "/news", "/feed/news/friends", "/feed/news/sources", "/feed/news/topics"]),
    Included(
        "Feed podcasts",
        NativeRouteDestinationId.FeedPodcasts,
        "/feed/podcasts",
        ["/feed/podcasts", "/podcast-episodes", "/feed/podcasts/friends", "/feed/podcasts/sources", "/feed/podcasts/topics"]),
    Included(
        "Feed videos",
        NativeRouteDestinationId.FeedVideos,
        "/feed/videos",
        ["/feed/videos", "/videos", "/feed/videos/friends", "/feed/videos/sources", "/feed/videos/topics"]),
    Included(
        "Feed referral links",
        NativeRouteDestinationId.FeedReferralLinks,
        "/feed/referral-links",
        ["/feed/referral-links", "/feed/referral-links/mutual"]),
    Included("Web search", NativeRouteDestinationId.WebSearch, "/web-search", ["/web-search"]),
    Included(
        "Posts browse",
        NativeRouteDestinationId.PostsBrowse,
        "/posts",
        ["/posts", "/reviews", "/discussions", "/articles", "/blog", "/data-points", "/links", "/curated-asides/topics"]),
    Included("Stories browse", NativeRouteDestinationId.StoriesBrowse, "/stories", ["/stories"]),
    Included(
        "Post compose",
        NativeRouteDestinationId.PostCompose,
        "/reviews/create",
        PostCreatePaths.Concat(["/communities/:slug/posts/create"])),
    Included("Post edit", NativeRouteDestinationId.PostCompose, "/review/123/edit", PostEditPatterns()),
    Included("Post detail", NativeRouteDestinationId.PostDetail, "/review/123", PostDetailPatterns()),
    Included(
        "Topic browse",
        NativeRouteDestinationId.TopicsBrowse,
        "/topics",
        [
          "/topics", "/admin/topic-claims", "/rss-feed-categories", "/cards",
          "/topic-claims/:topicId", "/rewards-programs", "/rewards-program-statuses", "/spending-categories",
        ]),
    Included("Referral programs", NativeRouteDestinationId.FeedReferralLinks, "/referral-programs", ["/referral-programs"]),
  ];
}

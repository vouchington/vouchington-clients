enum NativeRouteCatalogFeedsAndPosts {
    static let entries: [NativeRouteCatalogEntry] = [
        .included(
            destinationIdentifier: .feedPosts,
            representativePath: "/feed/posts",
            patterns: ["/feed", "/feed/posts", "/feed/posts/friends", "/feed/posts/topics"]
        ),
        .included(
            destinationIdentifier: .feedNews,
            representativePath: "/feed/news",
            patterns: ["/feed/news", "/news", "/feed/news/friends", "/feed/news/sources", "/feed/news/topics"]
        ),
        .included(
            destinationIdentifier: .feedPodcasts,
            representativePath: "/feed/podcasts",
            patterns: [
                "/feed/podcasts",
                "/podcast-episodes",
                "/feed/podcasts/friends",
                "/feed/podcasts/sources",
                "/feed/podcasts/topics"
            ]
        ),
        .included(
            destinationIdentifier: .feedVideos,
            representativePath: "/feed/videos",
            patterns: ["/feed/videos", "/videos", "/feed/videos/friends", "/feed/videos/sources", "/feed/videos/topics"]
        ),
        .included(
            destinationIdentifier: .feedReferralLinks,
            representativePath: "/feed/referral-links",
            patterns: ["/feed/referral-links", "/feed/referral-links/mutual"]
        ),
        .included(
            destinationIdentifier: .webSearch,
            representativePath: "/web-search",
            patterns: ["/web-search"]
        ),
        .included(
            destinationIdentifier: .fediverseSearch,
            representativePath: "/fediverse",
            patterns: ["/fediverse"]
        ),
        .included(
            destinationIdentifier: .fediverseInstances,
            representativePath: "/instances",
            patterns: ["/instances"]
        ),
        .included(
            destinationIdentifier: .postsBrowse,
            representativePath: "/posts",
            patterns: ["/posts", "/reviews", "/discussions", "/articles", "/blog", "/data-points", "/links"]
        ),
        .included(
            destinationIdentifier: .storiesBrowse,
            representativePath: "/stories",
            patterns: ["/stories"]
        ),
        .included(
            destinationIdentifier: .postCompose,
            representativePath: "/reviews/create",
            patterns: nativeRoutePatterns(
                NativeRouteCatalogPatterns.postCreatePaths + ["/communities/:slug/posts/create"]
            )
        ),
        .included(
            destinationIdentifier: .postDetail,
            representativePath: "/review/123",
            patterns: nativeRoutePatterns(NativeRouteCatalogPatterns.postTypes.flatMap { postType -> [String] in
                NativeRouteCatalogPatterns.postDetailSubpaths.map { "/\(postType)/:id\($0)" }
            } + ["/topic-recommendations/:id/comment/:commentId"])
        ),
        .included(
            destinationIdentifier: .rssFeedItemDetail,
            representativePath: "/rss-feed-items/123/tags/topic",
            patterns: nativeRoutePatterns([
                "/rss-feed-items/:id/tags/:objectType"
            ])
        ),
        .included(
            destinationIdentifier: .topicsBrowse,
            representativePath: "/topics",
            patterns: [
                "/topics",
                "/cards",
                "/rewards-programs",
                "/referral-programs",
                "/spending-categories"
            ]
        ),
        .included(
            destinationIdentifier: .topicImportExport,
            representativePath: "/my/topics/import-export",
            patterns: ["/my/topics/import-export"]
        )
    ]
}

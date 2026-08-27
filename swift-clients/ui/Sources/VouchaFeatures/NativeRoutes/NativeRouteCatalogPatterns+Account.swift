extension NativeRouteCatalogPatterns {
    static let bookmarkPaths = [
        "/my/posts/saved",
        "/my/posts/hidden",
        "/my/posts/following",
        "/my/posts/subscribed",
        "/my/topics/following",
        "/my/topics/muted",
        "/my/topics/blocked",
        "/my/topics/dismissed-recommendations",
        "/my/topics/viewed",
        "/my/users/following",
        "/my/users/followers",
        "/my/users/subscribed-posts",
        "/my/users/muted",
        "/my/users/blocked",
        "/my/friend-recommendations/dismissed",
        "/my/news-items/saved",
        "/my/news-items/hidden",
        "/my/news-items/viewed",
        "/my/news-sources/muted",
        "/my/news-sources/viewed",
        "/my/podcast-episodes/saved",
        "/my/podcast-episodes/hidden",
        "/my/podcast-episodes/viewed",
        "/my/podcasts/muted",
        "/my/podcasts/viewed",
        "/my/videos/saved",
        "/my/videos/hidden",
        "/my/videos/viewed",
        "/my/channels/muted",
        "/my/channels/viewed",
        "/my/urls/saved",
        "/my/domains/muted",
        "/my/domains/blocked",
        "/my/communities/saved",
        "/my/communities/proxy-following",
        "/my/communities/proxy-muted",
        "/my/rss-feed-items/saved",
        "/my/rss-feed-items/viewed",
        "/my/rss-feed-items/hidden"
    ]

    static let accountStatusPaths = [
        "/my/account-status",
        "/my/identity-verification"
    ]

    static let moderationCasePatterns = nativeRoutePatterns([
        "/appeals",
        "/disputes",
        "/my/appeals",
        "/my/disputes",
        "/my/warnings",
        "/my/bans",
        "/my/removed-posts"
    ])

    static var excludedTopicSettingsPatterns: [NativeRoutePattern] {
        nativeRoutePatterns(topicTypes.flatMap { topicType -> [String] in
            [
                "/\(topicType)/:idOrSlug/settings",
                "/\(topicType)/:idOrSlug/settings/**"
            ]
        })
    }

    static var topicManagementPatterns: [NativeRoutePattern] {
        nativeRoutePatterns(
            ["/topics/create"] +
                topicTypes.flatMap { topicType -> [String] in
                    [
                        "/\(topicType)/:idOrSlug/settings/about",
                        "/\(topicType)/:idOrSlug/settings/behavior",
                        "/\(topicType)/:idOrSlug/settings/domains",
                        "/\(topicType)/:idOrSlug/settings/source",
                        "/\(topicType)/:idOrSlug/settings/aliases",
                        "/\(topicType)/:idOrSlug/settings/merge"
                    ]
                }
        )
    }

    static var topicDetailPatterns: [NativeRoutePattern] {
        nativeRoutePatterns(topicTypes.flatMap { topicType -> [String] in
            let tagPaths = topicType == "source" ? [] : ["/\(topicType)/:idOrSlug/tags/:objectType"]
            return [
                "/\(topicType)/:idOrSlug"
            ] + topicSubpaths.map { "/\(topicType)/:idOrSlug\($0)" } + tagPaths
        })
    }

    static var userProfilePatterns: [NativeRoutePattern] {
        nativeRoutePatterns(["/user/:idOrUsername"] + profileSubpages.map { "/user/:idOrUsername\($0)" })
    }
}

func nativeRoutePatterns(_ templates: [String]) -> [NativeRoutePattern] {
    templates.map { NativeRoutePattern($0) }
}

enum NativeRouteCatalogPatterns {
    static let topicTypes = [
        "bank-account",
        "card",
        "instance",
        "referral-program",
        "rewards-program",
        "rewards-program-status",
        "source",
        "topic"
    ]

    static let topicSubpaths = [
        "/posts",
        "/discussions",
        "/reviews",
        "/data-points",
        "/referral-links",
        "/latest",
        "/news",
        "/articles",
        "/blog-posts",
        "/followers"
    ]

    static let postDetailSubpaths = [
        "",
        "/edit",
        "/comment/:commentId",
        "/tags/:objectType"
    ]

    static let postTypes = [
        "review",
        "discussion",
        "story",
        "article",
        "blog-post",
        "link",
        "data-point"
    ]

    static let postCreatePaths = [
        "/reviews/create",
        "/discussions/create",
        "/links/create",
        "/data-points/create",
        "/articles/create",
        "/blog/create"
    ]

    static let sourceDetailSubpaths = [
        "",
        "/latest",
        "/news",
        "/posts",
        "/discussions",
        "/reviews",
        "/data-points",
        "/articles",
        "/blog-posts",
        "/followers"
    ]

    static let landingPagePatterns = nativeRoutePatterns([
        "/landing/:idOrUsername",
        "/landing/:idOrUsername/:slug",
        "/user/:idOrUsername/landing",
        "/@:username",
        "/@:username/:slug"
    ])

    static let myLandingPagePatterns = nativeRoutePatterns([
        "/my/landing-page/:slug",
        "/my/landing-page/:slug/analytics"
    ])

    static let profileSubpages = [
        "/posts",
        "/discussions",
        "/reviews",
        "/comments",
        "/topics/following",
        "/users/following",
        "/users/followers",
        "/rss-feeds/following",
        "/communities/member"
    ]

    static var sourceDetailPatterns: [NativeRoutePattern] {
        nativeRoutePatterns(
            sourceDetailSubpaths.map { "/source/:idOrSlug\($0)" } +
                [
                    "/source/:idOrSlug/crawls",
                    "/source/:idOrSlug/crawls/:crawlId",
                    "/source/:idOrSlug/tags/:objectType"
                ]
        )
    }

    static var communityActionPatterns: [NativeRoutePattern] {
        nativeRoutePatterns([
            "/communities/create",
            "/communities/invite/:code",
            "/communities/:slug/apply"
        ])
    }

    static var communitySettingsPatterns: [NativeRoutePattern] {
        nativeRoutePatterns([
            "/communities/:slug/settings",
            "/communities/:slug/settings/applications",
            "/communities/:slug/settings/invites",
            "/communities/:slug/settings/pinned-posts",
            "/communities/:slug/settings/modlog",
            "/communities/:slug/settings/moderation",
            "/communities/:slug/settings/moderation/analytics",
            "/communities/:slug/settings/moderation/modmail",
            "/communities/:slug/settings/moderation/modmail/:threadId",
            "/communities/:slug/settings/**"
        ])
    }

    static var communityDetailPatterns: [NativeRoutePattern] {
        nativeRoutePatterns(
            communityActionPatterns.map(\.template) + [
                "/communities/:slug/about",
                "/communities/:slug/posts/create",
                "/communities/:slug/lists",
                "/communities/:slug/members",
                "/communities/:slug/news",
                "/communities/:slug/news/sources",
                "/communities/:slug/news/topics",
                "/communities/:slug/posts",
                "/communities/:slug/posts/pending",
                "/communities/:slug/pinned-posts",
                "/communities/:slug/applications",
                "/communities/:slug/invites",
                "/communities/:slug/bans",
                "/communities/:slug/restrictions",
                "/communities/:slug/modlog",
                "/communities/:slug/modmail",
                "/communities/:slug/modmail/:threadId",
                "/communities/:slug/moderator-stats",
                "/communities/:slug/moderator-vacation",
                "/communities/:slug/moderation-queue",
                "/communities/:slug/moderation-analytics",
                "/communities/:slug/ai-agents",
                "/communities/:slug/agent-prompts",
                "/communities/:slug/automod/**",
                "/communities/:slug/lists/:itemType"
            ] + communitySettingsPatterns.map(\.template) + ["/communities/:slug"]
        )
    }
}

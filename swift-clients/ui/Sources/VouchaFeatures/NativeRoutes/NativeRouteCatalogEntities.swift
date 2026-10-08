enum NativeRouteCatalogEntities {
    static let entries: [NativeRouteCatalogEntry] = [
        .included(
            destinationIdentifier: .sourceDetail,
            representativePath: "/source/example",
            patterns: NativeRouteCatalogPatterns.sourceDetailPatterns
        ),
        .included(
            destinationIdentifier: .topicDetail,
            representativePath: "/topic/hello-world",
            patterns: NativeRouteCatalogPatterns.topicDetailPatterns
        ),
        .included(
            destinationIdentifier: .topicManagement,
            representativePath: "/topics/create",
            patterns: NativeRouteCatalogPatterns.topicManagementPatterns
        ),
        .included(
            destinationIdentifier: .sourcesBrowse,
            representativePath: "/sources",
            patterns: [
                "/sources",
                "/news-sources",
                "/podcasts",
                "/podcasts/:category",
                "/channels",
                "/my/news-sources",
                "/my/podcasts",
                "/my/channels"
            ]
        ),
        .included(
            destinationIdentifier: .sourceImportExport,
            representativePath: "/my/sources/import-export",
            patterns: [
                "/my/news-sources/import-export",
                "/my/podcasts/import-export",
                "/my/channels/import-export",
                "/my/sources/import-export"
            ]
        ),
        .excluded(
            auditFamily: "RSS feed category admin",
            auditReason: "RSS feed category triage is admin-only and excluded from the native user catalog.",
            representativePath: "/rss-feed-categories",
            patterns: ["/rss-feed-categories"]
        ),
        .included(
            destinationIdentifier: .domainsBrowse,
            representativePath: "/domains",
            patterns: ["/domains"]
        ),
        .included(
            destinationIdentifier: .domainDetail,
            representativePath: "/domain/example.com",
            patterns: ["/domain/:idOrHostname"]
        ),
        .included(
            destinationIdentifier: .urlsBrowse,
            representativePath: "/urls",
            patterns: ["/urls"]
        ),
        .included(
            destinationIdentifier: .urlDetail,
            representativePath: "/url/url-1",
            patterns: ["/url/:id", "/url/:id/crawls", "/url/:id/crawls/:crawlId"]
        ),
        .included(
            destinationIdentifier: .usersBrowse,
            representativePath: "/users",
            patterns: ["/users", "/my/users/dismissed-recommendations"]
        ),
        .included(
            destinationIdentifier: .userProfile,
            representativePath: "/user/alice",
            patterns: NativeRouteCatalogPatterns.userProfilePatterns
        ),
        .included(
            destinationIdentifier: .communitiesBrowse,
            representativePath: "/communities",
            patterns: ["/communities", "/communities/lists"]
        ),
        .included(
            destinationIdentifier: .communityDetail,
            representativePath: "/communities/voucha",
            patterns: NativeRouteCatalogPatterns.communityDetailPatterns
        ),
        .included(
            destinationIdentifier: .messages,
            representativePath: "/messages/123",
            patterns: [
                "/messages",
                "/messages/new",
                "/messages/:conversationId",
                "/messages/modmail/:communitySlug/:threadId"
            ]
        ),
        .included(
            destinationIdentifier: .chat,
            representativePath: "/chat",
            patterns: [
                "/chat",
                "/chat/:id"
            ]
        ),
        .included(
            destinationIdentifier: .notifications,
            representativePath: "/my/notifications",
            patterns: ["/my/notifications"]
        )
    ]
}

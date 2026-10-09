enum NativeRouteCatalogAccount {
    static let entries: [NativeRouteCatalogEntry] = [
        .included(
            destinationIdentifier: .signIn,
            representativePath: "/login",
            patterns: ["/login"]
        ),
        .included(
            destinationIdentifier: .accountSettings,
            representativePath: "/my/identity",
            patterns: nativeRoutePatterns(
                ["/my/identity", "/my/privacy", "/my/membership"] +
                    NativeRouteCatalogPatterns.accountStatusPaths
            )
        ),
        .included(
            destinationIdentifier: .copyrightNotices,
            representativePath: "/copyright/notices",
            patterns: ["/copyright/notices", "/copyright/notices/:id"]
        ),
        .included(
            destinationIdentifier: .household,
            representativePath: "/my/household",
            patterns: ["/my/household"]
        ),
        .included(
            destinationIdentifier: .paymentCards,
            representativePath: "/my/cards",
            patterns: ["/my/cards"]
        ),
        .included(
            destinationIdentifier: .pointValuations,
            representativePath: "/my/rewards-program-point-valuations",
            patterns: ["/my/rewards-program-point-valuations"]
        ),
        .included(
            destinationIdentifier: .spendingCategories,
            representativePath: "/my/spending-categories",
            patterns: ["/my/spending-categories"]
        ),
        .included(
            destinationIdentifier: .rewardsProgramStatuses,
            representativePath: "/my/rewards-program-statuses",
            patterns: ["/my/rewards-program-statuses"]
        ),
        .included(
            destinationIdentifier: .profileSettings,
            representativePath: "/my/profile",
            patterns: ["/my/profile"]
        ),
        .included(
            destinationIdentifier: .advancedSettings,
            representativePath: "/my/preferences",
            patterns: [
                "/my/preferences",
                "/my/api-keys",
                "/my/data",
                "/my/language",
                "/my/news-preferences"
            ]
        ),
        .included(
            destinationIdentifier: .friendRecommendations,
            representativePath: "/my/friend-recommendations",
            patterns: ["/my/friend-recommendations"]
        ),
        .included(
            destinationIdentifier: .notificationSettings,
            representativePath: "/my/notification-settings",
            patterns: ["/my/notification-settings"]
        ),
        .included(
            destinationIdentifier: .referrals,
            representativePath: "/my/referrals",
            patterns: ["/my/referrals", "/my/referral-links"]
        ),
        .included(
            destinationIdentifier: .landingPages,
            representativePath: "/my/landing-pages",
            patterns: nativeRoutePatterns(["/my/landing-pages"]) +
                NativeRouteCatalogPatterns.myLandingPagePatterns +
                NativeRouteCatalogPatterns.landingPagePatterns
        ),
        .included(
            destinationIdentifier: .lists,
            representativePath: "/my/lists",
            patterns: nativeRoutePatterns(["/my/lists"])
        ),
        .included(
            destinationIdentifier: .bookmarks,
            representativePath: "/my/posts/saved",
            patterns: nativeRoutePatterns(NativeRouteCatalogPatterns.bookmarkPaths)
        ),
        .included(
            destinationIdentifier: .plans,
            representativePath: "/plans",
            patterns: ["/plans"]
        ),
        .included(
            destinationIdentifier: .topicRecommendations,
            representativePath: "/topic-recommendations",
            patterns: [
                "/topic-recommendations",
                "/topic-recommendations/create",
                "/topic-recommendations/:id",
                "/topic-recommendations/:id/edit"
            ]
        ),
        .included(
            destinationIdentifier: .moderationCases,
            representativePath: "/my/appeals",
            patterns: NativeRouteCatalogPatterns.moderationCasePatterns
        ),
        .included(
            destinationIdentifier: .moderationTransparency,
            representativePath: "/moderation-transparency",
            patterns: ["/moderation-transparency"]
        ),
        .included(
            destinationIdentifier: .compare,
            representativePath: "/compare",
            patterns: ["/compare", "/compare/:slugA-vs-:slugB", "/domains/compare"]
        )
    ]
}

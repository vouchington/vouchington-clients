enum NativeRouteCatalogStaff {
    static let entries: [NativeRouteCatalogEntry] = [
        .included(
            destinationIdentifier: .membershipGrants,
            representativePath: "/memberships/grants",
            patterns: ["/memberships/grants"]
        ),
        .included(
            destinationIdentifier: .userAdmin,
            representativePath: "/user/alice/admin",
            patterns: ["/user/:idOrUsername/admin"]
        ),
        .included(
            destinationIdentifier: .engineeringQueues,
            representativePath: "/admin/queues",
            patterns: ["/admin/queues"]
        ),
        .included(
            destinationIdentifier: .engineeringPostgresql,
            representativePath: "/admin/postgresql",
            patterns: ["/admin/postgresql"]
        ),
        .included(
            destinationIdentifier: .engineeringValkey,
            representativePath: "/admin/valkey",
            patterns: ["/admin/valkey"]
        ),
        .included(
            destinationIdentifier: .engineeringAiCosts,
            representativePath: "/admin/ai-costs",
            patterns: ["/admin/ai-costs"]
        ),
        .included(
            destinationIdentifier: .engineeringDynamicConfig,
            representativePath: "/admin/dynamic-config",
            patterns: ["/admin/dynamic-config"]
        ),
        .included(
            destinationIdentifier: .growthDashboard,
            representativePath: "/growth",
            patterns: ["/growth"]
        ),
        .included(
            destinationIdentifier: .moderationReports,
            representativePath: "/reports",
            patterns: ["/reports"]
        ),
        .included(
            destinationIdentifier: .moderationAppeals,
            representativePath: "/appeals",
            patterns: ["/appeals"]
        ),
        .included(
            destinationIdentifier: .moderationDisputes,
            representativePath: "/disputes",
            patterns: ["/disputes"]
        ),
        .included(
            destinationIdentifier: .moderationReviewQueue,
            representativePath: "/posts/review-queue",
            patterns: ["/posts/review-queue"]
        ),
        .included(
            destinationIdentifier: .moderationAdmin,
            representativePath: "/admin/modlog",
            patterns: ["/admin/modlog", "/admin/moderation-analytics"]
        ),
        .included(
            destinationIdentifier: .moderationIntegrity,
            representativePath: "/vote-integrity/flags",
            patterns: IntegrityRouteKind.allCases.map { NativeRoutePattern($0.rawValue) }
        )
    ]
}

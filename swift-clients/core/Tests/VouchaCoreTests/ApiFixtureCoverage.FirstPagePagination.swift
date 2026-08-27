import VouchaModels

let firstPagePaginationFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.my.api-keys.paginated") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<ApiKey>.self,
            ignoring: ["results.last_used_at", "results.revoked_at"]
        )
    },
    RegisteredFixture(id: "native.my.push-subscriptions.paginated") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<WebPushSubscription>.self,
            ignoring: [
                "results.__entity_type",
                "results.expiration_time_ms",
                "results.last_failure_at",
                "results.last_success_at"
            ]
        )
    },
    RegisteredFixture(id: "native.community.pending-reports.paginated") {
        try assertFixtureCoversDTO($0, as: CommunityPendingReportsResponse.self)
    },
    RegisteredFixture(id: "native.comments.descendants.default") {
        try assertFixtureCoversDTO(
            $0,
            as: PostThreadEnvelope.self,
            ignoring: descendantEnvelopeNullableAndSidecarFields
        )
    }
]

private let descendantEnvelopeNullableAndSidecarFields: Set<String> = [
    "election_votes.comment-a.__entity_type",
    "election_votes.comment-b.__entity_type",
    "page_info.end_cursor",
    "page_info.start_cursor",
    "post_elections.comment-a.__entity_type",
    "post_elections.comment-a.id",
    "post_elections.comment-b.__entity_type",
    "post_elections.comment-b.id",
    "post_elections.comment-c.__entity_type",
    "post_elections.comment-c.id",
    "posts.comment-a.__entity_type",
    "posts.comment-a.clearance_status",
    "posts.comment-a.created_by.profile_image_id",
    "posts.comment-a.slug",
    "posts.comment-b.__entity_type",
    "posts.comment-b.clearance_status",
    "posts.comment-b.created_by.profile_image_id",
    "posts.comment-b.slug",
    "posts.comment-c.__entity_type",
    "posts.comment-c.clearance_status",
    "posts.comment-c.created_by",
    "posts.comment-c.created_by_id",
    "posts.comment-c.slug",
    "posts.comment-d.__entity_type",
    "posts.comment-d.clearance_status",
    "posts.comment-d.created_by",
    "posts.comment-d.created_by_id",
    "posts.comment-d.markdown",
    "posts.comment-d.slug",
    "posts_metrics.comment-a.__entity_type",
    "posts_metrics.comment-a.bookmarks",
    "posts_metrics.comment-a.bookmarks.follow",
    "posts_metrics.comment-a.bookmarks.save",
    "posts_metrics.comment-a.id",
    "posts_metrics.comment-a.updated_at",
    "posts_metrics.comment-b.__entity_type",
    "posts_metrics.comment-b.bookmarks",
    "posts_metrics.comment-b.bookmarks.follow",
    "posts_metrics.comment-b.bookmarks.save",
    "posts_metrics.comment-b.id",
    "posts_metrics.comment-b.updated_at",
    "posts_metrics.comment-c.__entity_type",
    "posts_metrics.comment-c.bookmarks",
    "posts_metrics.comment-c.bookmarks.follow",
    "posts_metrics.comment-c.bookmarks.save",
    "posts_metrics.comment-c.id",
    "posts_metrics.comment-c.updated_at",
    "posts_metrics.comment-d.__entity_type",
    "posts_metrics.comment-d.bookmarks",
    "posts_metrics.comment-d.bookmarks.follow",
    "posts_metrics.comment-d.bookmarks.save",
    "posts_metrics.comment-d.id",
    "posts_metrics.comment-d.updated_at",
    "results.__entity_type",
    "results.id"
]

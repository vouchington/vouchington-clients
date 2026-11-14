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
    }
]

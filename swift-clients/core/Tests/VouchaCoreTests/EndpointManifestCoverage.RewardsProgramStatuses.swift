import VouchaAPI

extension EndpointManifestCoverage {
    static let rewardsProgramStatusEndpoints: [ManifestRegisteredEndpoint] = [
        .init(id: "native.rewards-program-status-topics.search.default") {
            ApiFixtureEndpointCoverageTests
                .rewardsProgramStatusFixtureEndpoints["native.rewards-program-status-topics.search.default"]!
        },
        .init(id: "native.rewards-statuses.empty") { .rewardsProgramStatuses() },
        .init(id: "native.rewards-statuses.page-1") { .rewardsProgramStatuses(limit: 2) },
        .init(id: "native.rewards-statuses.page-2") {
            ApiFixtureEndpointCoverageTests.rewardsProgramStatusFixtureEndpoints["native.rewards-statuses.page-2"]!
        },
        .init(id: "native.rewards-statuses.create.default") {
            ApiFixtureEndpointCoverageTests
                .rewardsProgramStatusFixtureEndpoints["native.rewards-statuses.create.default"]!
        },
        .init(id: "native.rewards-statuses.update.full") {
            ApiFixtureEndpointCoverageTests
                .rewardsProgramStatusFixtureEndpoints["native.rewards-statuses.update.full"]!
        },
        .init(id: "native.rewards-statuses.update.clear-dates") {
            ApiFixtureEndpointCoverageTests
                .rewardsProgramStatusFixtureEndpoints["native.rewards-statuses.update.clear-dates"]!
        },
        .init(id: "native.rewards-statuses.delete.default") {
            ApiFixtureEndpointCoverageTests
                .rewardsProgramStatusFixtureEndpoints["native.rewards-statuses.delete.default"]!
        }
    ]
}

import VouchaAPI

extension EndpointManifestCoverage {
    static let pointValuationEndpoints: [ManifestRegisteredEndpoint] = [
        .init(id: "native.rewards-program-topics.search.default") {
            .rewardsProgramTopics(query: "Travel")
        },
        .init(id: "native.point-valuations.empty") { .pointValuations() },
        .init(id: "native.point-valuations.page-1") { .pointValuations(limit: 2) },
        .init(id: "native.point-valuations.page-2") {
            .pointValuations(after: ApiFixtureEndpointCoverageTests.pointValuationPageCursor, limit: 2)
        },
        .init(id: "native.point-valuations.create.default") {
            .createPointValuation(body: ApiFixtureEndpointCoverageTests.createPointValuationBody)
        },
        .init(id: "native.point-valuations.update.full") {
            .updatePointValuation(
                id: ApiFixtureEndpointCoverageTests.firstPointValuationId,
                body: ApiFixtureEndpointCoverageTests.fullPointValuationUpdateBody
            )
        },
        .init(id: "native.point-valuations.update.clear-note") {
            .updatePointValuation(
                id: ApiFixtureEndpointCoverageTests.secondPointValuationId,
                body: ApiFixtureEndpointCoverageTests.clearPointValuationNoteBody
            )
        },
        .init(id: "native.point-valuations.delete.default") {
            .deletePointValuation(id: ApiFixtureEndpointCoverageTests.firstPointValuationId)
        }
    ]
}

import VouchaAPI

extension EndpointManifestCoverage {
    static let spendingCategoryEndpoints = ApiFixtureEndpointCoverageTests.spendingCategoryFixtureEndpoints
        .sorted { $0.key < $1.key }
        .map { id, endpoint in
            ManifestRegisteredEndpoint(id: id) { endpoint }
        }
}

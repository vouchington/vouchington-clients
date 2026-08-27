import VouchaAPI

extension EndpointManifestCoverage {
    static let moderationParityEndpoints: [ManifestRegisteredEndpoint] =
        ApiFixtureEndpointCoverageTests.moderationParityEndpointRegistry
            .map { id, endpoint in
                ManifestRegisteredEndpoint(id: id) { endpoint }
            }
}

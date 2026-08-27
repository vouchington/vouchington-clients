import VouchaAPI

extension EndpointManifestCoverage {
    static let followerDistributionEndpoints = followerDistributionFixtureEndpoints
        .sorted { $0.key < $1.key }
        .map { id, endpoint in
            ManifestRegisteredEndpoint(id: id) { endpoint }
        }
}

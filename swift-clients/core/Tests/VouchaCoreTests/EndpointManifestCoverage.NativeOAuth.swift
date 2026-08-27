import VouchaAPI

extension EndpointManifestCoverage {
    static let nativeOAuthAndFriendRecommendationEndpoints =
        ApiFixtureEndpointCoverageTests.nativeOAuthAndFriendRecommendationEndpointRegistrations.map {
            registration in
            ManifestRegisteredEndpoint(id: registration.id) {
                registration.endpoint
            }
        }
}

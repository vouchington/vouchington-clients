import VouchaAPI

struct ManifestRegisteredEndpoint {
    let id: String
    let makeEndpoint: () -> Endpoint
}

enum EndpointManifestCoverage {
    static let registry: [ManifestRegisteredEndpoint] =
        communityEndpoints
            + householdEndpoints
            + paymentCardEndpoints
            + pointValuationEndpoints
            + spendingCategoryEndpoints
            + rewardsProgramStatusEndpoints
            + entityRelationEndpoints
            + bookmarkEndpoints
            + referralEndpoints
            + moderationEndpoints
            + memberAppealEndpoints
            + moderationParityEndpoints
            + engineeringEndpoints
            + swiftCoreEndpoints
            + nativeContentEndpoints
            + nativeMessageEndpoints
            + membershipStoreEndpoints
            + importExportEndpoints
            + nativeOAuthAndFriendRecommendationEndpoints
            + followerDistributionEndpoints
            + storyEndpoints
            + fixtureMapEndpoints

    private static let fixtureMapEndpoints: [ManifestRegisteredEndpoint] = [
        communityAutomodFixtureEndpoints,
        currentContractFixtureEndpoints,
        entityProvenanceFixtureEndpoints,
        copyrightNoticesFixtureEndpoints,
        topicClaimFixtureEndpoints,
        nativeSessionFixtureEndpoints
    ].flatMap { endpoints in
        endpoints.map { id, endpoint in ManifestRegisteredEndpoint(id: id) { endpoint } }
    }
}

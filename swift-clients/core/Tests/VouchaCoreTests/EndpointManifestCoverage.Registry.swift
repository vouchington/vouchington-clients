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
            + moderationParityEndpoints
            + engineeringEndpoints
            + swiftCoreEndpoints
            + nativeContentEndpoints
            + nativeCrmEndpoints
            + nativeMessageEndpoints
            + membershipStoreEndpoints
            + importExportEndpoints
            + nativeOAuthAndFriendRecommendationEndpoints
            + followerDistributionEndpoints
}

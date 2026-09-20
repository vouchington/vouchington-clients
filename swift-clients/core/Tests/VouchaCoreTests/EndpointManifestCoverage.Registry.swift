import VouchaAPI

struct ManifestRegisteredEndpoint {
    let id: String
    let makeEndpoint: () -> Endpoint
}

enum EndpointManifestCoverage {
    static let nonSwiftManifestFixtureIds: Set<String> = [
        "web.oauth.authorization.complete.acknowledged"
    ]

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
            + nativeAgentConversationEndpoints
            + nativeMessageEndpoints
            + membershipStoreEndpoints
            + importExportEndpoints
            + nativeOAuthAndFriendRecommendationEndpoints
            + followerDistributionEndpoints
}

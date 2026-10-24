import VouchaModels

let nativeOAuthAndFriendRecommendationFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.oauth.providers.broker-capabilities") {
        try assertFixtureCoversDTO($0, as: OAuthBrokerCapabilitiesResponse.self)
    },
    RegisteredFixture(id: "native.oauth.authorization.begin") {
        try assertFixtureCoversDTO($0, as: BeginNativeOAuthAuthorizationResponse.self)
    },
    RegisteredFixture(id: "native.oauth.authorization.complete.pending") {
        try assertFixtureCoversDTO($0, as: NativeOAuthCompletionResponse.self)
    },
    RegisteredFixture(id: "native.oauth.authorization.complete.authenticated") {
        try assertFixtureCoversDTO($0, as: NativeOAuthCompletionResponse.self)
    },
    RegisteredFixture(id: "native.oauth.authorization.complete.mfa") {
        try assertFixtureCoversDTO($0, as: NativeOAuthCompletionResponse.self)
    },
    RegisteredFixture(id: "native.oauth.authorization.complete.connected") {
        try assertFixtureCoversDTO($0, as: NativeOAuthCompletionResponse.self)
    },
    RegisteredFixture(id: "native.friend-recommendations.default") {
        try assertFixtureCoversDTO(
            $0,
            as: FriendRecommendationsResponse.self,
            ignoring: ["users.user-1.profile_image_id"]
        )
    },
    RegisteredFixture(id: "native.friend-recommendations.second-page") {
        try assertFixtureCoversDTO(
            $0,
            as: FriendRecommendationsResponse.self,
            ignoring: [
                "users.019fafc1-308d-7db5-b834-6bbf641bf0e7.profile_image_id",
                "users.user-1.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "native.friend-recommendations.empty") {
        try assertFixtureCoversDTO($0, as: FriendRecommendationsResponse.self)
    }
]

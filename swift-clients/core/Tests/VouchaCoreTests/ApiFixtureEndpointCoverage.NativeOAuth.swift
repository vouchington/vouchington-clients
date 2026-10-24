@testable import VouchaAPI
import VouchaCore

extension ApiFixtureEndpointCoverageTests {
    static let nativeOAuthAndFriendRecommendationEndpointRegistrations: [(id: String, endpoint: Endpoint)] = [
        ("native.oauth.providers.broker-capabilities", .oauthProviders),
        ("native.oauth.authorization.begin", .beginNativeOAuthAuthorization(
            provider: .github,
            purpose: .authenticate,
            completionProofChallenge: "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ"
        )),
        ("native.oauth.authorization.complete.pending", nativeOAuthCompletionEndpoint),
        ("native.oauth.authorization.complete.authenticated", nativeOAuthCompletionEndpoint),
        ("native.oauth.authorization.complete.mfa", nativeOAuthCompletionEndpoint),
        ("native.oauth.authorization.complete.connected", nativeOAuthCompletionEndpoint),
        ("native.friend-recommendations.default", .friendRecommendations()),
        ("native.friend-recommendations.second-page", .friendRecommendations(
            after: "eyJpZCI6InVzZXItMSIsInNjb3BlIjoie1wicmVzb3VyY2VcIjpcIm15LWZyaWVuZC1yZWNvbW1lbmRhdGlvbnNcIixcIm93bmVyX2lkXCI6XCJmaXh0dXJlLXVzZXJcIixcIm9yZGVyXCI6XCJpZC1hc2NcIn0ifQ"
        )),
        ("native.friend-recommendations.empty", .friendRecommendations(
            after: "eyJpZCI6IjAxOWZhZmMxLTMwOGQtN2RiNS1iODM0LTZiYmY2NDFiZjBlNyIsInNjb3BlIjoie1wicmVzb3VyY2VcIjpcIm15LWZyaWVuZC1yZWNvbW1lbmRhdGlvbnNcIixcIm93bmVyX2lkXCI6XCJmaXh0dXJlLXVzZXJcIixcIm9yZGVyXCI6XCJpZC1hc2NcIn0ifQ"
        ))
    ]

    static let nativeOAuthAndFriendRecommendationFixtureEndpoints = Dictionary(
        uniqueKeysWithValues: nativeOAuthAndFriendRecommendationEndpointRegistrations.map {
            ($0.id, $0.endpoint)
        }
    )

    private static let nativeOAuthCompletionEndpoint = Endpoint.completeNativeOAuthAuthorization(
        flowId: "019fafb8-a44c-73e2-890a-497ff3dd27a6",
        completionToken: "native-completion-token",
        completionProofVerifier: "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV"
    )
}

import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeTopicRecommendationChallengeTests: XCTestCase {
    func testComposerChallengeUsesConfiguredSiteKeyCreateActionAndClientOrigin() throws {
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "https://staging.example/api")),
                turnstileSiteKey: "configured-key"
            ),
            cookieStorage: HTTPCookieStorage()
        )
        let model = NativeTopicRecommendationViewModel(client: client)
        let surface = NativeTopicRecommendationSurface(
            client: client, recommendationId: nil, turnstileSiteKey: "configured-key"
        )

        let challenge = surface.turnstileChallenge(viewModel: model)

        XCTAssertEqual(challenge.siteKey, "configured-key")
        XCTAssertEqual(challenge.action, .postsCreate)
        XCTAssertEqual(challenge.baseURL, client.baseURL)
        challenge.onToken("verified-token")
        XCTAssertEqual(model.turnstileToken, "verified-token")
    }
}

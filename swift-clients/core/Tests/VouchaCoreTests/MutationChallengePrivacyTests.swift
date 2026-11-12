import VouchaCore
import XCTest

final class MutationChallengePrivacyTests: XCTestCase {
    func testGenericProofsAreRemovedFromAllTelemetryFields() {
        let secrets = [
            "x-voucha-bot-challenge-token": "secret-token",
            "botChallengeProof": "secret-proof",
            "bot_challenge_assertion": "secret-assertion",
            "botChallengeNonce": "secret-nonce"
        ]
        let result = SentryEventScrubber.scrub(.init(
            headers: secrets, extras: secrets,
            breadcrumbs: [.init(category: "http", data: secrets)]
        ))
        XCTAssertTrue(result.headers.values.allSatisfy { $0 == SentryEventScrubber.filteredValue })
        XCTAssertTrue(result.extras.isEmpty)
        XCTAssertTrue(result.breadcrumbs.allSatisfy(\.data.isEmpty))
    }
}

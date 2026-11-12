import Foundation
import ViewInspector
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeTurnstileChallengeTests: XCTestCase {
    func testInvalidActionFailsBeforeCreatingChallenge() throws {
        let view = try NativeTurnstileChallengeView(
            siteKey: "key", action: .init(rawValue: "posts.<create>"),
            baseURL: XCTUnwrap(URL(string: "https://staging.example"))
        ) { _ in XCTFail("Invalid challenge must not emit tokens") }
        let expected = UiMessages.string(.nativeAuthVerificationTokenRequired, locale: .current)
        XCTAssertEqual(try view.inspect().find(text: expected).string(), expected)
    }

    func testMarkupCarriesMappedRouteAndEscapedSiteKey() throws {
        let context = try XCTUnwrap(try TurnstileChallengeContext(
            actionTag: AppAttestActionTag.communitiesCreatePost.rawValue,
            baseURL: XCTUnwrap(URL(string: "https://staging.example/api"))
        ))
        let html = turnstileHTML(siteKey: "key\"><script>", challenge: context)
        XCTAssertTrue(html.contains("data-action=\"communities-create-post\""))
        XCTAssertTrue(html.contains("data-sitekey=\"key&quot;&gt;&lt;script&gt;\""))
        XCTAssertTrue(html.contains("data-callback=\"onTurnstileToken\""))
        XCTAssertFalse(html.contains("key\"><script>"))
    }
}

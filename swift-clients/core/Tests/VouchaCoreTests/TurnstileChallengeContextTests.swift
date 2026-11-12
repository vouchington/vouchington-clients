import Foundation
import VouchaAuth
@testable import VouchaCore
import XCTest

final class TurnstileChallengeContextTests: XCTestCase {
    func testCanonicalActionMappingPreservesHyphensAndReplacesEveryDot() throws {
        let context = try XCTUnwrap(try TurnstileChallengeContext(
            actionTag: "auth.email-address-tokens",
            baseURL: XCTUnwrap(URL(string: "https://user:password@staging.example:8443/api?x=1#secret"))
        ))
        XCTAssertEqual(context.action, "auth-email-address-tokens")
        XCTAssertEqual(context.origin.absoluteString, "https://staging.example:8443")
        XCTAssertEqual(TurnstileChallengeContext(
            actionTag: "a.b.c-D_9", baseURL: context.origin
        )?.action, "a-b-c-D_9")
    }

    func testEveryProtectedRouteUsesItsCanonicalProviderAction() throws {
        let expected: [(AppAttestActionTag, String)] = [
            (.postsCreate, "posts-create"),
            (.communitiesCreate, "communities-create"),
            (.communitiesCreatePost, "communities-create-post"),
            (.disputesCreate, "disputes-create"),
            (.appealsCreate, "appeals-create"),
            (.authEmailAddressTokens, "auth-email-address-tokens"),
            (.reportsCreate, "reports-create")
        ]
        for (tag, action) in expected {
            XCTAssertEqual(try TurnstileChallengeContext(
                actionTag: tag.rawValue, baseURL: XCTUnwrap(URL(string: "https://staging.example"))
            )?.action, action)
        }
    }

    func testRejectsInvalidProviderActionsBeforeRendering() throws {
        let baseURL = try XCTUnwrap(URL(string: "https://staging.example"))
        for action in ["", "a b", "é", "a/b", "<script>", "a\n", String(repeating: "a", count: 33)] {
            XCTAssertNil(TurnstileChallengeContext(actionTag: action, baseURL: baseURL), action)
        }
        for action in ["a", String(repeating: "a", count: 32)] {
            XCTAssertNotNil(TurnstileChallengeContext(actionTag: action, baseURL: baseURL))
        }
        for rawURL in ["file:///tmp/widget", "voucha://challenge", "/relative"] {
            XCTAssertNil(try TurnstileChallengeContext(
                actionTag: "posts.create",
                baseURL: XCTUnwrap(URL(string: rawURL))
            ))
        }
    }

    func testWidgetAttributesEscapeProviderConfiguration() throws {
        let context = try XCTUnwrap(try TurnstileChallengeContext(
            actionTag: "posts.create", baseURL: XCTUnwrap(URL(string: "http://localhost:2999/path"))
        ))
        XCTAssertEqual(
            context.widgetAttributes(siteKey: "\"<&>'"),
            "data-sitekey=\"&quot;&lt;&amp;&gt;&#39;\" data-action=\"posts-create\""
        )
        XCTAssertEqual(context.origin.absoluteString, "http://localhost:2999")
    }
}

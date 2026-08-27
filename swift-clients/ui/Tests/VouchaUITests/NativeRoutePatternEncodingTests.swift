@testable import VouchaFeatures
import XCTest

final class NativeRoutePatternEncodingTests: XCTestCase {
    func testPlaceholderCapturesDecodeEncodedRouteValuesOnce() throws {
        let pattern: NativeRoutePattern = "/agent/:idOrSlug/conversation/:conversationId"
        let match = try XCTUnwrap(
            pattern.match("/agent/foo%20bar%2Fbaz%3Aqux%25%E2%9C%93/conversation/conversation%2Fone")
        )

        XCTAssertEqual(match.param("idOrSlug"), "foo bar/baz:qux%✓")
        XCTAssertEqual(match.param("conversationId"), "conversation/one")
    }

    func testEmbeddedPlaceholderCapturesDecodeEncodedRouteValuesOnce() throws {
        let pattern: NativeRoutePattern = "/@:username"
        let match = try XCTUnwrap(pattern.match("/@alice%20bob%3A%25%E2%9C%93"))

        XCTAssertEqual(match.param("username"), "alice bob:%✓")
    }

    func testMalformedPlaceholderEscapeFallsBackToOriginalCapture() throws {
        let pattern: NativeRoutePattern = "/agent/:idOrSlug"
        let match = try XCTUnwrap(pattern.match("/agent/foo%2Gbar"))

        XCTAssertEqual(match.param("idOrSlug"), "foo%2Gbar")
    }

    func testLiteralPlusStaysPlusAndDoubleEncodedSlashDecodesOnce() throws {
        let pattern: NativeRoutePattern = "/agent/:idOrSlug"
        let match = try XCTUnwrap(
            pattern.match("/agent/foo+bar%252Fbaz?username=alice+smith&post_slug=one%2Btwo")
        )

        XCTAssertEqual(match.param("idOrSlug"), "foo+bar%2Fbaz")
        XCTAssertEqual(match.queryValue("username"), "alice smith")
        XCTAssertEqual(match.queryValue("post_slug"), "one+two")
    }

    func testMalformedQueryEscapesKeepTheirValueAfterPlusDecoding() throws {
        let pattern: NativeRoutePattern = "/agents"
        let match = try XCTUnwrap(pattern.match("/agents?username=alice+%2Gsmith"))

        XCTAssertEqual(match.queryValue("username"), "alice %2Gsmith")
    }

    func testAppLinksPreserveEncodedValuesUntilRouteMatchingDecodesOnce() throws {
        let agentURL = try XCTUnwrap(URL(string: "https://voucha.example/agent/foo%252fbar"))
        let agentPattern: NativeRoutePattern = "/agent/:idOrSlug"
        let agentMatch = try XCTUnwrap(agentPattern.match(nativeAppLinkRoutePathAndQuery(agentURL)))
        XCTAssertEqual(agentMatch.param("idOrSlug"), "foo%2fbar")

        let customSchemeURL = try XCTUnwrap(URL(string: "VOUCHA://agent/foo%252fbar"))
        XCTAssertEqual(nativeAppLinkRoutePathAndQuery(customSchemeURL), "/agent/foo%252fbar")

        let searchURL = try XCTUnwrap(URL(string: "https://voucha.example/search?q=C%2B%2B"))
        XCTAssertEqual(nativeAppLinkRouteQuery(searchURL), "q=C%2B%2B")
        let searchPattern: NativeRoutePattern = "/search"
        let searchMatch = try XCTUnwrap(searchPattern.match(nativeAppLinkRoutePathAndQuery(searchURL)))
        XCTAssertEqual(searchMatch.queryValue("q"), "C++")
    }

    func testDirectRoutesKeepTheirExistingSingleDecodeContract() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agent/foo%252fbar"))

        XCTAssertEqual(route.match.param("idOrSlug"), "foo%2fbar")
    }

    func testSplatCaptureUsesTheSameDecodingContractAsPlaceholders() throws {
        let pattern: NativeRoutePattern = "/communities/:slug/automod/**"
        let match = try XCTUnwrap(pattern.match("/communities/foo%20bar/automod/rule%2Fone/%E2%9C%93"))

        XCTAssertEqual(match.param("slug"), "foo bar")
        XCTAssertEqual(match.param("splat"), "rule/one/✓")
    }
}

import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import VouchaTestSupport
import XCTest

@MainActor
final class NativeChatMCPFeedPostFactTests: XCTestCase {
    func testCanonicalDeliveryRendersOnlyPostIdentifierWithLocalizedCopy() throws {
        let value = try canonicalResult()
        guard case let .feedPostIds(ids)? = NativeChatMCPFact.makeFeedPostIds(value) else {
            return XCTFail("Canonical feed delivery was not recognized")
        }
        XCTAssertEqual(ids, ["0198ffff-0000-7000-8000-000000000002"])
        for locale in [Locale(identifier: "en"), Locale(identifier: "fr")] {
            let view = try NativeChatFeedPostFactView(postIds: ids, locale: locale).inspect()
            XCTAssertNoThrow(try view.find(text: UiMessages.string(.nativeSwiftRouteSurfacePosts, locale: locale)))
            XCTAssertNoThrow(try view.find(text: UiMessages.string(.nativeSwiftCommunitiesPostId, locale: locale)))
            XCTAssertNoThrow(try view.find(text: ids[0]))
        }
    }

    func testInvalidOrFailedDeliveryDoesNotBecomeFact() throws {
        let value = try canonicalResult()
        guard case var .object(fields) = value else { return XCTFail("Expected object") }
        fields["success"] = .bool(false)
        XCTAssertNil(NativeChatMCPFact.makeFeedPostIds(.object(fields)))
        fields["success"] = .bool(true)
        fields["results"] = .array([.object(["entity_id": .string("untrusted text")])])
        XCTAssertNil(NativeChatMCPFact.makeFeedPostIds(.object(fields)))
        let id = "0198ffff-0000-7000-8000-000000000002"
        fields["results"] = .array([.object(["entity_id": .string(id)]), .object(["entity_id": .string(id)])])
        XCTAssertNil(NativeChatMCPFact.makeFeedPostIds(.object(fields)))
    }

    private func canonicalResult() throws -> DecodedJSONValue {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp-results.json"])
        let data = try Data(contentsOf: root.appendingPathComponent("api-fixtures/v1/mcp-results.json"))
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let cases = try XCTUnwrap(object["cases"] as? [[String: Any]])
        let row = try XCTUnwrap(cases.first { $0["id"] as? String == "native.mcp.read-feed.posts" })
        let structured = try XCTUnwrap(row["structuredContent"])
        return try DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: structured))
    }
}

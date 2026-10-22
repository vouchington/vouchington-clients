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
final class NativeChatMCPFeedRssItemFactTests: XCTestCase {
    func testCanonicalDeliveryRendersOnlyRssItemIdentifierWithLocalizedCopy() throws {
        let value = try canonicalResult()
        guard case let .feedRssItemIds(ids)? = NativeChatMCPFact.make(
            tool: "read_feed", option: .string("rss_items"), content: value
        ) else {
            return XCTFail("Canonical RSS delivery was not recognized")
        }
        XCTAssertEqual(ids, ["0198ffff-0000-7000-8000-000000000002"])
        guard case .feedPostIds? = NativeChatMCPFact.make(
            tool: "read_feed", option: .string("posts"), content: value
        ) else {
            return XCTFail("The same delivery shape remains a post fact only for the posts option")
        }
        XCTAssertNil(NativeChatMCPFact.make(tool: "read_feed", option: .string("referral_links"), content: value))
        for locale in [Locale(identifier: "en"), Locale(identifier: "fr")] {
            let view = try NativeChatFeedRssItemFactView(itemIds: ids, locale: locale).inspect()
            XCTAssertNoThrow(try view.find(text: UiMessages.string(.nativeSwiftRouteSurfaceRssItem, locale: locale)))
            XCTAssertNoThrow(try view.find(text: UiMessages.string(label(ids[0]), locale: locale)))
            XCTAssertThrowsError(try view.find(text: "shared_by_id"))
        }
    }

    func testEmptyDeliveryUsesExistingNoResultsCopy() throws {
        var value = try canonicalResult()
        guard case var .object(fields) = value else { return XCTFail("Expected object") }
        fields["results"] = .array([])
        value = .object(fields)
        guard case let .feedRssItemIds(ids)? = NativeChatMCPFact.makeFeedRssItemIds(value) else {
            return XCTFail("An empty successful delivery is still a fact")
        }
        XCTAssertTrue(ids.isEmpty)
        let locale = Locale(identifier: "en")
        let view = try NativeChatFeedRssItemFactView(itemIds: ids, locale: locale).inspect()
        XCTAssertNoThrow(try view.find(text: UiMessages.string(.nativeSwiftRouteSurfaceNoResults, locale: locale)))
    }

    func testInvalidOrFailedDeliveryDoesNotBecomeFact() throws {
        let value = try canonicalResult()
        guard case var .object(fields) = value else { return XCTFail("Expected object") }
        fields["success"] = .bool(false)
        XCTAssertNil(NativeChatMCPFact.makeFeedRssItemIds(.object(fields)))
        fields["success"] = .bool(true)
        fields["results"] = .array([.object(["entity_id": .string("untrusted text")])])
        XCTAssertNil(NativeChatMCPFact.makeFeedRssItemIds(.object(fields)))
        let id = "0198ffff-0000-7000-8000-000000000002"
        fields["results"] = .array([.object(["entity_id": .string(id)]), .object(["entity_id": .string(id)])])
        XCTAssertNil(NativeChatMCPFact.makeFeedRssItemIds(.object(fields)))
    }

    func testReceiptKeepsCallIdentityAndRejectsOtherAccountOrTurn() throws {
        let fact = try XCTUnwrap(NativeChatMCPFact.makeFeedRssItemIds(canonicalResult()))
        let owner = NativeChatMCPTurnOwner(
            accountId: "account-a", revision: 2, conversationId: "conversation-1", userMessageId: "turn-2"
        )
        var ledger = NativeChatMCPReceiptLedger(owner: owner)
        for stale in [
            NativeChatMCPTurnOwner(
                accountId: "account-b", revision: 2, conversationId: "conversation-1", userMessageId: "turn-2"
            ),
            NativeChatMCPTurnOwner(
                accountId: "account-a", revision: 1, conversationId: "conversation-1", userMessageId: "turn-2"
            ),
            NativeChatMCPTurnOwner(
                accountId: "account-a", revision: 2, conversationId: "conversation-1", userMessageId: "turn-1"
            )
        ] {
            XCTAssertFalse(ledger.record(.init(
                id: UUID(), callId: "reused-call", owner: stale,
                toolName: "read_feed", envelope: "synthetic rss envelope", kind: .response, fact: fact
            )))
        }
        XCTAssertTrue(ledger.receipts.isEmpty)
        XCTAssertTrue(ledger.record(.init(
            id: UUID(), callId: "rss-call", owner: owner,
            toolName: "read_feed", envelope: "synthetic rss envelope", kind: .response, fact: fact
        )))
        XCTAssertEqual(ledger.receipts.map(\.callId), ["rss-call"])
        XCTAssertEqual(ledger.receipts.first?.fact, fact)
    }

    private func label(_ itemId: String) -> UiMessage {
        UiMessage(.nativeSwiftIntegrityRssItemId, textParameters: ["id": .protocolValue(itemId)])
    }

    private func canonicalResult() throws -> DecodedJSONValue {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp-results.json"])
        let data = try Data(contentsOf: root.appendingPathComponent("api-fixtures/v1/mcp-results.json"))
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let cases = try XCTUnwrap(object["cases"] as? [[String: Any]])
        let row = try XCTUnwrap(cases.first { $0["id"] as? String == "native.mcp.read-feed.rss-items" })
        let structured = try XCTUnwrap(row["structuredContent"])
        return try DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: structured))
    }
}

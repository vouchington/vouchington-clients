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
final class NativeChatMCPFeedReferralLinkFactTests: XCTestCase {
    func testCanonicalEmptyDeliveryRendersReferralEmptyCopy() throws {
        let value = try canonicalResult()
        guard case let .feedReferralLinks(links)? = NativeChatMCPFact.make(
            tool: "read_feed", option: .string("referral_links"), content: value
        ) else {
            return XCTFail("Canonical referral delivery was not recognized")
        }
        XCTAssertTrue(links.isEmpty)
        guard case .feedPostIds? = NativeChatMCPFact.make(
            tool: "read_feed", option: .string("posts"), content: value
        ) else {
            return XCTFail("An empty success remains a post fact only for the posts option")
        }
        XCTAssertNil(NativeChatMCPFact.make(tool: "read_feed", option: .string("rss_items"), content: populated()))
        for locale in [Locale(identifier: "en"), Locale(identifier: "fr")] {
            let view = try NativeChatFeedReferralLinkFactView(links: links, locale: locale).inspect()
            XCTAssertNoThrow(try view.find(text: UiMessages.string(
                .nativeSwiftLandingPagesReferralLinks, locale: locale
            )))
            XCTAssertNoThrow(try view.find(text: UiMessages.string(
                .nativeSwiftEmptyStateNoReferralLinks, locale: locale
            )))
        }
    }

    func testSyntheticPopulatedLinkRendersProgramAndUrlOnly() throws {
        guard case let .feedReferralLinks(links)? = NativeChatMCPFact.makeFeedReferralLinks(populated()) else {
            return XCTFail("A synthetic populated referral link should be a fact")
        }
        XCTAssertEqual(links, [NativeChatFeedReferralLinkFact(
            id: "link-1", programName: "Example Card", url: "https://example.test/r/card"
        )])
        let locale = Locale(identifier: "en")
        let view = try NativeChatFeedReferralLinkFactView(links: links, locale: locale).inspect()
        XCTAssertNoThrow(try view.find(text: UiMessages.string(
            .nativeSwiftReferralLinksManagementReferralProgram, locale: locale
        )))
        XCTAssertNoThrow(try view.find(text: "Example Card"))
        XCTAssertNoThrow(try view.find(text: UiMessages.string(
            .nativeSwiftReferralLinksManagementReferralUrl, locale: locale
        )))
        XCTAssertNoThrow(try view.find(text: "https://example.test/r/card"))
        XCTAssertThrowsError(try view.find(text: "user-1"))
        XCTAssertThrowsError(try view.find(text: "program-1"))
    }

    func testInvalidReferralDeliveryDoesNotBecomeFact() {
        XCTAssertNil(NativeChatMCPFact.makeFeedReferralLinks(populated(success: false)))
        XCTAssertNil(NativeChatMCPFact.makeFeedReferralLinks(populated(url: "javascript:alert(1)")))
        XCTAssertNil(NativeChatMCPFact.makeFeedReferralLinks(populated(url: "https://user:secret@example.test/r")))
        XCTAssertNil(NativeChatMCPFact.makeFeedReferralLinks(populated(programName: "  ")))
        guard case var .object(fields) = populated() else { return XCTFail("Expected object") }
        guard case let .array(rows) = fields["results"] else { return XCTFail("Expected results") }
        fields["results"] = .array(rows + rows)
        XCTAssertNil(NativeChatMCPFact.makeFeedReferralLinks(.object(fields)))
    }

    func testReceiptKeepsCallIdentityAndRejectsOtherAccountOrTurn() throws {
        let fact = try XCTUnwrap(NativeChatMCPFact.makeFeedReferralLinks(canonicalResult()))
        let owner = NativeChatMCPTurnOwner(
            accountId: "account-a", revision: 2, conversationId: "conversation-1", userMessageId: "turn-2"
        )
        var ledger = NativeChatMCPReceiptLedger(owner: owner)
        for stale in [
            NativeChatMCPTurnOwner(
                accountId: "account-b", revision: 2, conversationId: "conversation-1", userMessageId: "turn-2"
            ),
            NativeChatMCPTurnOwner(
                accountId: "account-a", revision: 2, conversationId: "conversation-1", userMessageId: "turn-1"
            )
        ] {
            XCTAssertFalse(ledger.record(.init(
                id: UUID(), callId: "reused-call", owner: stale,
                toolName: "read_feed", envelope: "synthetic referral envelope", kind: .response, fact: fact
            )))
        }
        XCTAssertTrue(ledger.receipts.isEmpty)
        XCTAssertTrue(ledger.record(.init(
            id: UUID(), callId: "referral-call", owner: owner,
            toolName: "read_feed", envelope: "synthetic referral envelope", kind: .response, fact: fact
        )))
        XCTAssertEqual(ledger.receipts.map(\.callId), ["referral-call"])
        XCTAssertEqual(ledger.receipts.first?.fact, fact)
    }

    private func canonicalResult() throws -> DecodedJSONValue {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp-results.json"])
        let data = try Data(contentsOf: root.appendingPathComponent("api-fixtures/v1/mcp-results.json"))
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let cases = try XCTUnwrap(object["cases"] as? [[String: Any]])
        let row = try XCTUnwrap(cases.first { $0["id"] as? String == "native.mcp.read-feed.referral-links" })
        let structured = try XCTUnwrap(row["structuredContent"])
        return try DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: structured))
    }

    private func populated(
        success: Bool = true, programName: String = "Example Card", url: String = "https://example.test/r/card"
    ) -> DecodedJSONValue {
        .object([
            "success": .bool(success),
            "results": .array([.object([
                "id": .string("link-1"),
                "user_id": .string("user-1"),
                "referral_program_id": .string("program-1"),
                "referral_program_name": .string(programName),
                "referral_program_slug": .string("example-card"),
                "url": .string(url),
                "label": .null
            ])])
        ])
    }
}

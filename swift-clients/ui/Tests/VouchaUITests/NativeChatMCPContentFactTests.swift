import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import VouchaTestSupport
import XCTest

/// Post, RSS item and story facts for both providers. The HTTP harness drives the real provider and
/// view model; the bridge that selects facts is shared with the built-in provider.
@MainActor
final class NativeChatMCPContentFactTests: NativeRouteSurfaceViewModelTestCase {
    static let postIds = [
        "native.mcp.read-posts.hacker-news-search", "native.mcp.read-posts.hacker-news-details"
    ]
    static let rssIds = [
        "native.mcp.read-rss-item.hacker-news-list", "native.mcp.read-rss-item.hacker-news-details",
        "native.mcp.get-story.hacker-news-members"
    ]
    private static var allIds: [String] {
        postIds + rssIds
    }

    private let english = Locale(identifier: "en")
    private let persistencePath = "/api/v1/conversations/conversation-1/client-generated-chat"

    func testFixturesDecodeToTypedFactsWithOnlyPublicFields() throws {
        for id in Self.postIds {
            guard case let .postSummaries(posts) = try XCTUnwrap(NativeChatMCPHTTPFixture(id).builtInFact, id) else {
                return XCTFail(id)
            }
            XCTAssertEqual(posts.map(\.title), ["Example post"], id)
            XCTAssertEqual(posts.first?.references?.storyCount, 2, id)
            XCTAssertEqual(posts.first?.references?.commentCount, 3, id)
        }
        for id in Self.rssIds {
            guard case let .rssItemSummaries(items) = try XCTUnwrap(NativeChatMCPHTTPFixture(id).builtInFact, id) else {
                return XCTFail(id)
            }
            XCTAssertEqual(items.map(\.title), ["Example article"], id)
            XCTAssertEqual(items.map(\.feedTitle), ["Example feed"], id)
            XCTAssertEqual(items.first?.references?.commentCount, 3, id)
        }
        let details = try NativeChatMCPHTTPFixture("native.mcp.read-posts.hacker-news-details")
        guard case let .postSummaries(posts) = try XCTUnwrap(details.builtInFact) else { return XCTFail() }
        XCTAssertEqual(posts.first?.references?.stories?.first?.points, 4)
        XCTAssertEqual(posts.first?.references?.comments?.first?.parentStoryItemId, 3)
    }

    func testResultsWithoutHackerNewsReferencesStillProduceTitleFacts() throws {
        let row: [String: Any] = ["id": "0198ffff-0000-7000-8000-000000000002", "title": "Plain", "post_type": "link"]
        let value = try DecodedJSONValue(exactJSONData: JSONSerialization.data(
            withJSONObject: ["success": true, "results": [row], "page_info": [:]]
        ))
        guard case let .postSummaries(posts) = try XCTUnwrap(
            NativeChatMCPFact.make(tool: "read_posts", option: .string("search"), content: value)
        ) else { return XCTFail() }
        XCTAssertNil(posts.first?.references)
    }

    func testMalformedContentProducesNoFact() throws {
        let valid = "0198ffff-0000-7000-8000-000000000002"
        let blankTitle: [String: Any] = ["id": valid, "title": "  "]
        let badId: [String: Any] = ["id": "not-a-uuid", "title": "A"]
        let item: [String: Any] = ["id": valid, "title": "A"]
        let badCount: [String: Any] = ["id": valid, "title": "A", "hacker_news_references": [
            "story_count": -1, "comment_count": 0
        ]]
        let payloads: [[String: Any]] = [
            ["success": false, "error": "denied"], ["success": true],
            ["success": true, "results": [blankTitle]], ["success": true, "results": [badId]],
            ["success": true, "results": [item, item]], ["success": true, "results": [badCount]],
            ["success": true, "results": [["id": valid, "title": "A", "hacker_news_references": [
                "story_count": 1, "comment_count": 1, "stories": [["hacker_news_item_id": 1, "posted_at": "x"]]
            ]]]]
        ]
        for payload in payloads {
            let value = try DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: payload))
            XCTAssertNil(NativeChatMCPFact.make(tool: "read_posts", option: .string("search"), content: value))
            XCTAssertNil(NativeChatMCPFact.make(tool: "read_rss_feed_item", option: .string("list"), content: value))
            XCTAssertNil(NativeChatMCPFact.make(tool: "get_story", option: nil, content: value))
        }
        let oversized = (0 ..< 101).map { _ in ["id": UUID().uuidString, "title": "A"] }
        let value = try DecodedJSONValue(exactJSONData: JSONSerialization.data(
            withJSONObject: ["success": true, "results": oversized]
        ))
        XCTAssertNil(NativeChatMCPFact.make(tool: "read_posts", option: .string("search"), content: value))
    }

    func testHTTPResultsRenderPublicFieldsOnlyAndNoRawJSON() async throws {
        for id in Self.allIds {
            CannedFeedURLProtocol.reset()
            let fixture = try NativeChatMCPHTTPFixture(id)
            let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
            CannedFeedURLProtocol.handlers[persistencePath] = (
                NativeChatTitleProviderTests.clientGeneratedChatData, 200
            )
            CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [
                fixture.call("call-1"), NativeChatMCPHTTPFactTests.answer("Done")
            ]
            await harness.send()
            let ledger = try XCTUnwrap(harness.viewModel.mcpReceiptLedger, id)
            let receipt = try XCTUnwrap(ledger.receipts.first, id)
            XCTAssertEqual(receipt.callId, "call-1", id)
            XCTAssertEqual(receipt.owner.accountId, "member-a", id)
            XCTAssertEqual(receipt.fact, fixture.builtInFact, id)
            let texts = try renderedTexts(ledger)
            let seen = UiMessages.string(
                UiMessage(.sharedHackerNewsSeenOnCount, numberParameters: ["count": 2]), locale: english
            )
            XCTAssertTrue(texts.contains(seen), id)
            XCTAssertTrue(texts.contains(Self.postIds.contains(id) ? "Example post" : "Example article"), id)
            for text in texts {
                for forbidden in [
                    "{", "}", "structuredContent", "\"success\"", "0198ffff", "example.test", "article-guid",
                    "Example post text", "Example article text", "example-post"
                ] {
                    XCTAssertFalse(text.contains(forbidden), "\(id) rendered \(forbidden): \(text)")
                }
            }
            XCTAssertEqual(harness.service.calls, 1, id)
        }
    }

    func testDetailsRenderStoryAndCommentReferences() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-rss-item.hacker-news-details")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
        CannedFeedURLProtocol.handlers[persistencePath] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [
            fixture.call("call-1"), NativeChatMCPHTTPFactTests.answer("Done")
        ]
        await harness.send()
        let texts = try renderedTexts(XCTUnwrap(harness.viewModel.mcpReceiptLedger))
        XCTAssertTrue(texts.contains(UiMessages.string(.sharedHackerNewsSeenOnHeading, locale: english)))
        XCTAssertTrue(texts.contains(UiMessages.string(.sharedHackerNewsMentionedOnHeading, locale: english)))
    }

    func testStaleAccountOutputIsDroppedForEveryFamily() async throws {
        for id in Self.allIds {
            CannedFeedURLProtocol.reset()
            let fixture = try NativeChatMCPHTTPFixture(id)
            let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
            harness.service.holdCalls = true
            CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [
                fixture.call("call-1"), NativeChatMCPHTTPFactTests.answer("Done")
            ]
            let send = Task { await harness.send() }
            await harness.service.waitForCall()
            let switching = Task {
                await harness.session.useAccount("member-b")
                await harness.session.useAccount("member-a")
            }
            harness.service.releaseCall()
            await switching.value
            await send.value
            XCTAssertNil(harness.viewModel.mcpReceiptLedger, id)
            XCTAssertFalse(harness.viewModel.messages.contains { $0.role == .assistant }, id)
        }
    }

    func testCanceledRefusedAndInvalidResultsProduceNoFacts() async throws {
        for id in Self.allIds {
            CannedFeedURLProtocol.reset()
            let fixture = try NativeChatMCPHTTPFixture(id)
            let canceled = try await NativeChatMCPHTTPHarness(fixture: fixture)
            canceled.service.holdCalls = true
            CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [
                fixture.call("call-1"), NativeChatMCPHTTPFactTests.answer("Done")
            ]
            let send = Task { await canceled.send() }
            await canceled.service.waitForCall()
            canceled.viewModel.abortStreaming()
            canceled.service.releaseCall()
            await send.value
            XCTAssertTrue(canceled.viewModel.mcpReceiptLedger?.receipts.compactMap(\.fact).isEmpty ?? true, id)

            CannedFeedURLProtocol.reset()
            let refused = try await NativeChatMCPHTTPHarness(fixture: fixture, isError: true)
            CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [
                fixture.call("call-1"), NativeChatMCPHTTPFactTests.answer("Done")
            ]
            await refused.send()
            let receipt = try XCTUnwrap(refused.viewModel.mcpReceiptLedger?.receipts.first, id)
            XCTAssertEqual(receipt.kind, .toolError, id)
            XCTAssertNil(receipt.fact, id)

            CannedFeedURLProtocol.reset()
            let invalid = try await NativeChatMCPHTTPHarness(
                fixture: fixture, structured: ["success": true, "results": [["title": "untrusted"]], "items": "x"]
            )
            CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [
                fixture.call("call-1"), NativeChatMCPHTTPFactTests.answer("Done")
            ]
            await invalid.send()
            XCTAssertTrue(invalid.viewModel.mcpReceiptLedger?.receipts.compactMap(\.fact).isEmpty ?? true, id)
        }
    }

    func testFactsSurviveModelContinuationFailure() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.get-story.hacker-news-members")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
        let failed = try (JSONSerialization.data(withJSONObject: ["status": "failed", "output": []]), 200, 0.0)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), failed]
        await harness.send()
        let ledger = try XCTUnwrap(harness.viewModel.mcpReceiptLedger)
        XCTAssertEqual(ledger.continuation, .processingStopped)
        XCTAssertEqual(ledger.receipts.first?.callId, "call-1")
        XCTAssertEqual(ledger.receipts.first?.fact, fixture.builtInFact)
        XCTAssertNotNil(fixture.builtInFact)
    }

    private func renderedTexts(_ ledger: NativeChatMCPReceiptLedger) throws -> [String] {
        let view = try NativeChatToolResultStatusView(ledger: ledger).inspect()
        return try view.findAll(ViewType.Text.self).map { try $0.string() }
    }
}

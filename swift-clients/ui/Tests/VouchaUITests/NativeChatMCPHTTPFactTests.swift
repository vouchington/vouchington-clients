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

/// HTTP (OpenAI-compatible) provider facts, driven through the real provider and view model with
/// canonical `mcp-results.json` results. The built-in provider tests assert the same fact values.
@MainActor
final class NativeChatMCPHTTPFactTests: NativeRouteSurfaceViewModelTestCase {
    static let familyIds = [
        "native.mcp.read-my-preferences.email", "native.mcp.read-my-preferences.general",
        "native.mcp.read-my-profile.bio", "native.mcp.read-my-profile.links",
        "native.mcp.read-feed.posts", "native.mcp.read-feed.rss-items", "native.mcp.read-feed.referral-links"
    ]
    private let persistencePath = "/api/v1/conversations/conversation-1/client-generated-chat"

    func testHTTPResultsRenderTheSameFactsAsBuiltInWithoutRawJSON() async throws {
        for id in Self.familyIds {
            CannedFeedURLProtocol.reset()
            let fixture = try NativeChatMCPHTTPFixture(id)
            let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
            CannedFeedURLProtocol.handlers[persistencePath] = (
                NativeChatTitleProviderTests.clientGeneratedChatData,
                200
            )
            CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), Self.answer("Done")]
            await harness.send()
            let ledger = try XCTUnwrap(harness.viewModel.mcpReceiptLedger, id)
            let receipt = try XCTUnwrap(ledger.receipts.first, id)
            XCTAssertEqual(ledger.receipts.count, 1, id)
            XCTAssertEqual(receipt.callId, "call-1", id)
            XCTAssertEqual(receipt.owner.accountId, "member-a", id)
            XCTAssertEqual(receipt.owner.userMessageId, ledger.owner.userMessageId, id)
            XCTAssertEqual(receipt.kind, .response, id)
            XCTAssertNotNil(receipt.fact, id)
            XCTAssertEqual(receipt.fact, fixture.builtInFact, id)
            let texts = try renderedTexts(ledger)
            XCTAssertTrue(texts.contains(UiMessages.string(
                .sharedChatMcpResultReceived,
                locale: Locale(identifier: "en")
            )))
            XCTAssertGreaterThan(texts.count, 2, id)
            for text in texts {
                for forbidden in ["{", "}", "structuredContent", "\"success\"", "isError"] {
                    XCTAssertFalse(text.contains(forbidden), "\(id) rendered raw data: \(text)")
                }
            }
            XCTAssertEqual(harness.service.calls, 1, id)
        }
    }

    func testAccountSwitchAToBToAInFlightDropsTheLateResult() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-my-preferences.general")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
        harness.service.holdCalls = true
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), Self.answer("Done")]
        let send = Task { await harness.send() }
        await harness.service.waitForCall()
        let switching = Task {
            await harness.session.useAccount("member-b")
            await harness.session.useAccount("member-a")
        }
        harness.service.releaseCall()
        await switching.value
        await send.value
        XCTAssertNil(harness.viewModel.mcpReceiptLedger)
        XCTAssertEqual(persistedRequests, 0)
        XCTAssertFalse(harness.viewModel.messages.contains { $0.role == .assistant })
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/v1/responses"), 1)
        XCTAssertEqual(harness.service.calls, 1)
    }

    func testCanceledTurnPublishesNoFact() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-feed.posts")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
        harness.service.holdCalls = true
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), Self.answer("Done")]
        let send = Task { await harness.send() }
        await harness.service.waitForCall()
        harness.viewModel.abortStreaming()
        harness.service.releaseCall()
        await send.value
        XCTAssertTrue(harness.viewModel.mcpReceiptLedger?.receipts.compactMap(\.fact).isEmpty ?? true)
        XCTAssertEqual(persistedRequests, 0)
    }

    func testRefusedResultKeepsEnvelopeButProducesNoFact() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-feed.posts")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture, isError: true)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), Self.answer("Done")]
        await harness.send()
        let receipts = try XCTUnwrap(harness.viewModel.mcpReceiptLedger?.receipts)
        XCTAssertEqual(receipts.count, 1)
        XCTAssertEqual(receipts.first?.kind, .toolError)
        XCTAssertNil(receipts.first?.fact)
        XCTAssertTrue(try XCTUnwrap(receipts.first?.envelope).contains("\"isError\":true"))
    }

    func testInvalidResultProducesNoFact() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-feed.posts")
        let invalid: [String: Any] = ["success": true, "results": [["entity_id": "untrusted text"]]]
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture, structured: invalid)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), Self.answer("Done")]
        await harness.send()
        XCTAssertTrue(harness.viewModel.mcpReceiptLedger?.receipts.compactMap(\.fact).isEmpty ?? true)
    }

    func testCompletedActionStatusPersistsWhenModelContinuationFails() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-feed.referral-links")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
        let failed = try (JSONSerialization.data(withJSONObject: ["status": "failed", "output": []]), 200, 0.0)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), failed]
        await harness.send()
        let ledger = try XCTUnwrap(harness.viewModel.mcpReceiptLedger)
        XCTAssertEqual(ledger.continuation, .processingStopped)
        XCTAssertEqual(ledger.receipts.count, 1)
        XCTAssertEqual(ledger.receipts.first?.callId, "call-1")
        XCTAssertEqual(ledger.receipts.first?.fact, fixture.builtInFact)
        XCTAssertEqual(harness.service.calls, 1)
        XCTAssertEqual(persistedRequests, 0)
        XCTAssertFalse(harness.viewModel.isStreaming)
        XCTAssertEqual(harness.viewModel.draftMessage, "")
        let texts = try renderedTexts(ledger)
        XCTAssertTrue(texts.contains(UiMessages.string(
            .sharedChatMcpModelProcessingStopped,
            locale: Locale(identifier: "en")
        )))
        XCTAssertFalse(texts.contains(UiMessages.string(
            .sharedChatMcpPossibleSideEffect,
            locale: Locale(identifier: "en")
        )))
    }

    func testReusedCallIdentifierIsRejectedAfterTheFirstCompletedCall() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-feed.posts")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("same"), fixture.call("same")]
        await harness.send()
        let ledger = try XCTUnwrap(harness.viewModel.mcpReceiptLedger)
        XCTAssertEqual(ledger.receipts.map(\.callId), ["same"])
        XCTAssertEqual(ledger.continuation, .processingStopped)
        XCTAssertEqual(harness.service.calls, 1)
    }

    func testNextTurnReplacesFactsEvenWhenTheProviderReusesACallIdentifier() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-feed.posts")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
        CannedFeedURLProtocol.handlers[persistencePath] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("reused"), Self.answer("One")]
        await harness.send()
        let first = try XCTUnwrap(harness.viewModel.mcpReceiptLedger)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("reused"), Self.answer("Two")]
        await harness.send(text: "Read the feed again")
        let second = try XCTUnwrap(harness.viewModel.mcpReceiptLedger)
        XCTAssertNotEqual(first.owner.userMessageId, second.owner.userMessageId)
        XCTAssertEqual(second.receipts.count, 1)
        XCTAssertNotEqual(first.receipts.first?.id, second.receipts.first?.id)
        XCTAssertEqual(harness.service.calls, 2)
    }

    func testPersistenceOnlyRetryKeepsHTTPFactsWithOneToolDispatch() async throws {
        let fixture = try NativeChatMCPHTTPFixture("native.mcp.read-feed.rss-items")
        let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
        CannedFeedURLProtocol.handlers[persistencePath] = (NativeChatTestFixtures.errorData, 400)
        CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), Self.answer("Done")]
        await harness.send()
        let receipt = try XCTUnwrap(harness.viewModel.mcpReceiptLedger?.receipts.first)
        XCTAssertEqual(persistedRequests, 1)
        CannedFeedURLProtocol.handlers[persistencePath] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        await harness.send()
        XCTAssertEqual(persistedRequests, 2)
        XCTAssertEqual(harness.service.calls, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/v1/responses"), 2)
        XCTAssertEqual(harness.viewModel.mcpReceiptLedger?.receipts.first, receipt)
    }

    private var persistedRequests: Int {
        CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == persistencePath }.count
    }

    private func renderedTexts(_ ledger: NativeChatMCPReceiptLedger) throws -> [String] {
        let view = try NativeChatToolResultStatusView(ledger: ledger).inspect()
        return try view.findAll(ViewType.Text.self).map { try $0.string() }
    }

    static func answer(_ text: String) throws -> (Data, Int, TimeInterval) {
        try (JSONSerialization.data(withJSONObject: ["output": [], "output_text": text]), 200, 0)
    }
}

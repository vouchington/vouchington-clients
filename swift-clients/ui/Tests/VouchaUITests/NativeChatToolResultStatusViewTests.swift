import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeChatToolResultStatusViewTests: XCTestCase {
    func testReceivedAndToolErrorRowsDoNotInferDomainSuccessOrExposeRawEnvelope() throws {
        var ledger = ledger()
        ledger.continuation = .contextStopped
        let view = NativeChatToolResultStatusView(ledger: ledger).environment(\.locale, Locale(identifier: "en"))
        let inspected = try view.inspect()
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .sharedChatMcpResultReceived,
            locale: Locale(identifier: "en")
        )))
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .sharedChatMcpToolErrorReceived,
            locale: Locale(identifier: "en")
        )))
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .sharedChatMcpModelContextStopped,
            locale: Locale(identifier: "en")
        )))
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .sharedChatMcpResultNotSaved,
            locale: Locale(identifier: "en")
        )))
        XCTAssertThrowsError(try inspected.find(text: "untrusted_tool_name"))
        XCTAssertThrowsError(try inspected.find(text: ledger.receipts[0].envelope))
    }

    func testStoppedAndTemporaryFooterFollowNativeFrenchLocale() throws {
        var ledger = ledger()
        ledger.continuation = .processingStopped
        let french = Locale(identifier: "fr")
        let inspected = try NativeChatToolResultStatusView(ledger: ledger).environment(\.locale, french).inspect()
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .sharedChatMcpModelProcessingStopped,
            locale: french
        )))
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(.sharedChatMcpResultNotSaved, locale: french)))
        XCTAssertThrowsError(try inspected.find(text: UiMessages.string(
            .sharedChatMcpModelContextStopped,
            locale: french
        )))
    }

    func testActualChatDetailKeepsAmbiguityWarningBesideEarlierReceivedResult() throws {
        var ledger = ledger()
        ledger.continuation = .ambiguous
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil, currentMemberId: ledger.owner.accountId)
        viewModel.messages = [.init(id: ledger.owner.userMessageId, role: .user, content: "Fixture request")]
        viewModel.mcpReceiptLedger = ledger
        viewModel.streamErrorMessage = .message(.sharedChatMcpPossibleSideEffect)
        let inspected = try NativeChatSurface(client: nil, routeMatch: nil).chatDetail(viewModel: viewModel).inspect()
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .sharedChatMcpPossibleSideEffect,
            locale: Locale(identifier: "en")
        )))
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .sharedChatMcpResultReceived,
            locale: Locale(identifier: "en")
        )))
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .sharedChatMcpResultNotSaved,
            locale: Locale(identifier: "en")
        )))
    }

    private func ledger() -> NativeChatMCPReceiptLedger {
        let owner = NativeChatMCPTurnOwner(
            accountId: "member-a",
            revision: 1,
            conversationId: "conversation-1",
            userMessageId: "local-user-1"
        )
        var ledger = NativeChatMCPReceiptLedger(owner: owner)
        _ = ledger.record(.init(
            id: UUID(),
            owner: owner,
            toolName: "untrusted_tool_name",
            envelope: #"{"content":[],"structuredContent":{"success":false},"isError":false}"#,
            kind: .response
        ))
        _ = ledger.record(.init(
            id: UUID(),
            owner: owner,
            toolName: "untrusted_tool_name",
            envelope: #"{"content":[],"isError":true}"#,
            kind: .toolError
        ))
        return ledger
    }
}

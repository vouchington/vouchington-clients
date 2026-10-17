import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeChatToolResultStatusViewTests: XCTestCase {
    func testActualChatDetailRendersGeneralFactBesideTheSameTemporaryReceiptWarning() throws {
        let fixture = try NativeChatMCPGeneralPreferenceFixture()
        let fact = try XCTUnwrap(try NativeChatMCPFact.makeGeneralPreferences(fixture.value))
        var ledger = ledger()
        XCTAssertTrue(ledger.record(.init(
            id: UUID(), callId: "general-preferences-call", owner: ledger.owner,
            toolName: "read_my_preferences", envelope: "untrusted raw general envelope",
            kind: .response, fact: fact
        )))
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil, currentMemberId: ledger.owner.accountId)
        viewModel.messages = [.init(
            id: ledger.owner.userMessageId,
            role: .user,
            content: "Fixture preferences request"
        )]
        viewModel.mcpReceiptLedger = ledger
        let inspected = try NativeChatSurface(client: nil, routeMatch: nil).chatDetail(viewModel: viewModel).inspect()
        let english = Locale(identifier: "en")
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(.settingsLanguageCountryLabel, locale: english)))
        XCTAssertNoThrow(try inspected.find(text: "US"))
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(.sharedChatMcpResultNotSaved, locale: english)))
        XCTAssertThrowsError(try inspected.find(text: "untrusted raw general envelope"))
    }

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
        XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
            .nativeSwiftSettingsEngagementEmails,
            locale: Locale(identifier: "en")
        )))
        XCTAssertNoThrow(try inspected.find(text: "09:30"))
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
            id: UUID(), callId: "fixture-call-1",
            owner: owner,
            toolName: "untrusted_tool_name",
            envelope: #"{"content":[],"structuredContent":{"success":false},"isError":false}"#,
            kind: .response, fact: Self.emailPreferenceFact
        ))
        _ = ledger.record(.init(
            id: UUID(), callId: "fixture-call-2",
            owner: owner,
            toolName: "untrusted_tool_name",
            envelope: #"{"content":[],"isError":true}"#,
            kind: .toolError, fact: nil
        ))
        return ledger
    }

    private static let emailPreferenceFact: NativeChatMCPFact? = {
        let payload = #"{"success":true,"email_preferences":{"is_engagement_emails_enabled":true,"news_digest_frequency":"weekly","is_moderation_emails_enabled":false,"community_digest_frequency":"none","moderation_email_cadence":"selected_days","moderation_email_days_of_week":[1,3,5],"moderation_email_time_of_day":"09:30","moderation_email_timezone":"Europe/Paris"}}"#
        guard let value = try? DecodedJSONValue(exactJSONData: Data(payload.utf8)) else { return nil }
        return NativeChatMCPFact.makeEmailPreferences(value)
    }()
}

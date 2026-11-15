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

/// Financial profile, bookmark, copyright notice and repeat-infringer facts. The HTTP harness drives the
/// real provider and view model; the bridge that selects facts is shared with the built-in provider.
@MainActor
final class NativeChatMCPAccountFactTests: NativeRouteSurfaceViewModelTestCase {
    static let ids = [
        "native.mcp.get-my-financial-profile.null", "native.mcp.get-my-financial-profile.populated",
        "native.mcp.manage-bookmark.set", "native.mcp.manage-bookmark.remove",
        "native.mcp.search-copyright-notices-for-association.results",
        "native.mcp.list-repeat-infringer-accounts.accounts"
    ]
    private let english = Locale(identifier: "en")
    private let persistencePath = "/api/v1/conversations/conversation-1/client-generated-chat"

    func testFixturesDecodeToTypedFacts() throws {
        XCTAssertEqual(
            try NativeChatMCPHTTPFixture("native.mcp.get-my-financial-profile.null").builtInFact,
            .financialProfile(nil)
        )
        guard case let .financialProfile(profile?) = try XCTUnwrap(
            NativeChatMCPHTTPFixture("native.mcp.get-my-financial-profile.populated").builtInFact
        ) else { return XCTFail("Expected a populated profile") }
        XCTAssertEqual(profile.creditScoreRange, "670-739")
        XCTAssertEqual(profile.statedIncomeRange?.minimum.amount, 7_500_000)
        XCTAssertEqual(profile.statedIncomeRange?.maximum.amount, 10_000_000)
        XCTAssertEqual(profile.yearsOfCreditHistory, 7)
        XCTAssertNil(profile.totalCreditLimit)
        XCTAssertNil(profile.hardInquiries12m)
        XCTAssertEqual(
            try NativeChatMCPHTTPFixture("native.mcp.manage-bookmark.set").builtInFact,
            .bookmark(.init(action: .set, predicate: .follow, entityTypes: [.topic]))
        )
        XCTAssertEqual(
            try NativeChatMCPHTTPFixture("native.mcp.manage-bookmark.remove").builtInFact,
            .bookmark(.init(action: .removed, predicate: .follow, entityTypes: [.topic]))
        )
        guard case let .copyrightNotices(notices) = try XCTUnwrap(NativeChatMCPHTTPFixture(
            "native.mcp.search-copyright-notices-for-association.results"
        ).builtInFact) else { return XCTFail("Expected notices") }
        XCTAssertEqual(notices.map(\.claimant), ["Example Claimant"])
        XCTAssertEqual(notices.map(\.workDescription), ["Example work"])
        XCTAssertEqual(notices.map(\.jurisdiction), [.usDMCA])
        guard case let .repeatInfringers(accounts) = try XCTUnwrap(NativeChatMCPHTTPFixture(
            "native.mcp.list-repeat-infringer-accounts.accounts"
        ).builtInFact) else { return XCTFail("Expected accounts") }
        XCTAssertEqual(accounts.map(\.username), ["example-account"])
        XCTAssertEqual(accounts.map(\.isOperative), [true])
        XCTAssertEqual(accounts.map(\.terminationInEffect), [false])
        XCTAssertEqual(accounts.map(\.hasOpenReview), [true])
        XCTAssertEqual(accounts.map(\.incidentCount), [1])
    }

    func testBookmarkUnknownPredicateOrEntityTypeProducesNoFact() throws {
        let set = try value(["success": true, "results": [["entity_type": "topic", "entity": "x", "status": "set"]]])
        let unknownType = try value([
            "success": true,
            "results": [["entity_type": "widget", "entity": "x", "status": "set"]]
        ])
        let skipped = try value([
            "success": true,
            "results": [["entity_type": "topic", "entity": "x", "status": "skipped"]]
        ])
        func make(
            _ option: String,
            _ arguments: [String: Any],
            _ content: DecodedJSONValue
        ) throws -> NativeChatMCPFact? {
            try NativeChatMCPFact.make(
                tool: "manage_bookmark", option: .string(option), arguments: value(arguments), content: content
            )
        }
        XCTAssertNotNil(try make("set", ["predicate": "mute"], set))
        XCTAssertNil(try make("set", ["predicate": "pin"], set))
        XCTAssertNil(try make("set", [:], set))
        XCTAssertNil(try make("set", ["predicate": "mute"], unknownType))
        XCTAssertNil(try make("set", ["predicate": "mute"], skipped))
        XCTAssertNil(NativeChatMCPFact.make(tool: "manage_bookmark", option: .string("set"), content: set))
        let removeContent = try value(["success": true])
        let remove: [String: Any] = ["predicate": "block", "entity_type": "url_hostname", "entity_id": "x"]
        XCTAssertNotNil(try make("remove", remove, removeContent))
        XCTAssertNil(try make("remove", remove.merging(["predicate": "pin"]) { $1 }, removeContent))
        XCTAssertNil(try make("remove", remove.merging(["entity_type": "widget"]) { $1 }, removeContent))
        XCTAssertNil(try make("remove", remove, value(["success": false])))
        XCTAssertNil(try make("remove", remove, set))
    }

    func testMalformedAccountContentProducesNoFact() throws {
        let id = "0198ffff-0000-7000-8000-000000000031"
        let notice: [String: Any] = [
            "id": id, "claimant_display_name": NSNull(), "jurisdiction": "us_dmca",
            "received_at": "2026-10-09T00:00:00.000Z", "work_description": NSNull()
        ]
        XCTAssertNotNil(try copyright([notice]))
        XCTAssertNil(try copyright([notice.merging(["jurisdiction": "uk"]) { $1 }]))
        XCTAssertNil(try copyright([notice.merging(["jurisdiction": "xx"]) { $1 }]))
        XCTAssertNil(try copyright([notice.merging(["id": "nope"]) { $1 }]))
        XCTAssertNil(try copyright([notice, notice]))
        XCTAssertNil(try copyright([notice.merging(["received_at": "yesterday"]) { $1 }]))
        XCTAssertNil(try copyright((0 ..< 101).map { _ in notice.merging(["id": UUID().uuidString]) { $1 } }))
        let profile: [String: Any] = ["credit_score_range": NSNull(), "years_of_credit_history": 3]
        XCTAssertNotNil(try financial(profile))
        XCTAssertEqual(try financial(["credit_score_range": NSNull()]), .financialProfile(nil))
        XCTAssertNil(try financial(["credit_score_range": "999-999"]))
        XCTAssertNil(try financial(["years_of_credit_history": -1]))
        XCTAssertNil(try financial(["total_credit_limit": ["amount": 1, "currency": "xyz"]]))
        XCTAssertNil(try financial(["stated_income_range": [
            "minimum": ["amount": 5, "currency": "usd"], "maximum": ["amount": 1, "currency": "usd"]
        ]]))
        let account: [String: Any] = [
            "account_user_id": id, "account_username": NSNull(), "incident_id": id,
            "is_operative": false, "open_review_id": NSNull(), "termination_in_effect": false
        ]
        XCTAssertNotNil(try accounts([account]))
        XCTAssertNil(try accounts([account.merging(["account_user_id": "nope"]) { $1 }]))
        XCTAssertNil(try accounts([account.merging(["incident_id": "nope"]) { $1 }]))
        XCTAssertNil(try accounts([account.merging(["is_operative": "yes"]) { $1 }]))
        XCTAssertNil(try NativeChatMCPFact.make(
            tool: "list_repeat_infringer_accounts", option: nil, content: value(["success": false])
        ))
    }

    func testRepeatInfringerIncidentsGroupPerAccountAndPickSingularOrPlural() throws {
        let one = "0198ffff-0000-7000-8000-000000000031"
        let two = "0198ffff-0000-7000-8000-000000000032"
        func row(_ incident: String, username: String?) -> [String: Any] {
            [
                "account_user_id": one, "account_username": username ?? NSNull(), "incident_id": incident,
                "is_operative": false, "open_review_id": NSNull(), "termination_in_effect": true
            ]
        }
        guard case let .repeatInfringers(grouped) = try XCTUnwrap(accounts([
            row(one, username: nil), row(two, username: "later-name")
        ])) else { return XCTFail("Expected accounts") }
        XCTAssertEqual(grouped.count, 1)
        XCTAssertEqual(grouped.first?.incidentCount, 2)
        XCTAssertEqual(grouped.first?.username, "later-name")
        let plural = try renderedTexts(.repeatInfringers(grouped))
        XCTAssertTrue(plural.contains(UiMessages.string(.nativeMcpFactsRepeatInfringersIncidents, locale: english)))
        XCTAssertFalse(plural.contains(UiMessages.string(.nativeMcpFactsRepeatInfringersIncident, locale: english)))
        guard case let .repeatInfringers(single) = try XCTUnwrap(accounts([row(one, username: "a")]))
        else { return XCTFail("Expected accounts") }
        let singular = try renderedTexts(.repeatInfringers(single))
        XCTAssertTrue(singular.contains(UiMessages.string(.nativeMcpFactsRepeatInfringersIncident, locale: english)))
    }

    func testHTTPResultsRenderLocalizedFactsWithoutRawJSONOrIdentifiers() async throws {
        let expected: [String: [String]] = [
            "native.mcp.get-my-financial-profile.null": [
                UiMessages.string(.nativeMcpFactsFinancialProfileEmpty, locale: english)
            ],
            "native.mcp.get-my-financial-profile.populated": [
                UiMessages.string(.nativeMcpFactsFinancialProfileCreditScoreRange, locale: english), "670-739",
                UiMessages.string(.nativeMcpFactsFinancialProfileStatedIncomeRange, locale: english),
                UiMessages.string(.nativeMcpFactsFinancialProfileYearsOfCreditHistory, locale: english), "7"
            ],
            "native.mcp.manage-bookmark.set": [
                UiMessages.string(.nativeMcpFactsBookmarkSet, locale: english), "Follow · Topic"
            ],
            "native.mcp.manage-bookmark.remove": [
                UiMessages.string(.nativeMcpFactsBookmarkRemoved, locale: english), "Follow · Topic"
            ],
            "native.mcp.search-copyright-notices-for-association.results": [
                UiMessages.string(.nativeMcpFactsCopyrightNoticesSearchTitle, locale: english),
                "Example Claimant", "Example work", "DMCA"
            ],
            "native.mcp.list-repeat-infringer-accounts.accounts": [
                UiMessages.string(.nativeMcpFactsRepeatInfringersTitle, locale: english), "example-account",
                UiMessages.string(.nativeMcpFactsRepeatInfringersIncident, locale: english)
            ]
        ]
        for id in Self.ids {
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
            XCTAssertNotNil(receipt.fact, id)
            let texts = try renderedTexts(ledger)
            for text in expected[id] ?? [] {
                XCTAssertTrue(texts.contains(text), "\(id) missing \(text) in \(texts)")
            }
            for text in texts {
                for forbidden in [
                    "{", "}", "structuredContent", "\"success\"", "individual_id", "0198ffff", "7500000",
                    "10000000", "subject_id", "created_by", "account_user_id", "incident_id", "usd", "us_dmca"
                ] {
                    XCTAssertFalse(text.contains(forbidden), "\(id) rendered \(forbidden): \(text)")
                }
            }
            XCTAssertEqual(harness.service.calls, 1, id)
        }
    }

    func testFinancialProfileRendersOnlyTheUsersOwnLabeledFields() throws {
        let fact = try XCTUnwrap(NativeChatMCPHTTPFixture("native.mcp.get-my-financial-profile.populated").builtInFact)
        let texts = try renderedTexts(fact)
        let labels = [
            UiMessages.string(.nativeMcpFactsFinancialProfileTotalCreditLimit, locale: english),
            UiMessages.string(.nativeMcpFactsFinancialProfileHardInquiries12m, locale: english),
            UiMessages.string(.nativeMcpFactsFinancialProfileCardsOpened24m, locale: english)
        ]
        for label in labels {
            XCTAssertFalse(texts.contains(label), "Null fields must not render \(label)")
        }
        XCTAssertFalse(texts.joined().contains("individual"))
        XCTAssertTrue(texts.contains { $0.contains("75,000") && $0.contains("100,000") })
    }

    func testLedgerRejectsForeignAccountAndTurnFacts() throws {
        let fact = try XCTUnwrap(NativeChatMCPHTTPFixture("native.mcp.get-my-financial-profile.populated").builtInFact)
        var ledger = NativeChatMCPReceiptLedger(owner: Self.owner)
        for foreign in [
            NativeChatMCPTurnOwner(
                accountId: "account-b",
                revision: 2,
                conversationId: "conversation-1",
                userMessageId: "turn-2"
            ),
            NativeChatMCPTurnOwner(
                accountId: "account-a",
                revision: 2,
                conversationId: "conversation-1",
                userMessageId: "turn-1"
            ),
            NativeChatMCPTurnOwner(
                accountId: "account-a",
                revision: 1,
                conversationId: "conversation-1",
                userMessageId: "turn-2"
            )
        ] {
            XCTAssertFalse(ledger.record(Self.receipt(fact, owner: foreign)))
        }
        XCTAssertTrue(ledger.receipts.isEmpty)
        XCTAssertTrue(ledger.record(Self.receipt(fact)))
        XCTAssertEqual(ledger.receipts.first?.callId, "call-1")
    }

    func testStaleAccountOutputIsDroppedForEveryFamily() async throws {
        for id in Self.ids {
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
        for id in Self.ids {
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
                fixture: fixture, structured: ["success": true, "unexpected": ["individual_id": "x"]]
            )
            CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [
                fixture.call("call-1"), NativeChatMCPHTTPFactTests.answer("Done")
            ]
            await invalid.send()
            XCTAssertTrue(invalid.viewModel.mcpReceiptLedger?.receipts.compactMap(\.fact).isEmpty ?? true, id)
        }
    }

    func testFactsSurviveModelContinuationFailure() async throws {
        for id in Self.ids {
            CannedFeedURLProtocol.reset()
            let fixture = try NativeChatMCPHTTPFixture(id)
            let harness = try await NativeChatMCPHTTPHarness(fixture: fixture)
            let failed = try (JSONSerialization.data(withJSONObject: ["status": "failed", "output": []]), 200, 0.0)
            CannedFeedURLProtocol.queuedHandlers["/v1/responses"] = try [fixture.call("call-1"), failed]
            await harness.send()
            let ledger = try XCTUnwrap(harness.viewModel.mcpReceiptLedger, id)
            XCTAssertEqual(ledger.continuation, .processingStopped, id)
            XCTAssertEqual(ledger.receipts.first?.callId, "call-1", id)
            XCTAssertEqual(ledger.receipts.first?.owner.accountId, "member-a", id)
            XCTAssertNotNil(ledger.receipts.first?.fact, id)
            XCTAssertEqual(ledger.receipts.first?.fact, fixture.builtInFact, id)
        }
    }

    private func value(_ object: Any) throws -> DecodedJSONValue {
        try DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: object))
    }

    private func copyright(_ rows: [[String: Any]]) throws -> NativeChatMCPFact? {
        try NativeChatMCPFact.make(
            tool: "search_copyright_notices_for_association", option: nil,
            content: value(["copyright_notices": rows, "page_info": [:]])
        )
    }

    private func accounts(_ rows: [[String: Any]]) throws -> NativeChatMCPFact? {
        try NativeChatMCPFact.make(
            tool: "list_repeat_infringer_accounts", option: nil,
            content: value(["copyright_repeat_infringer_accounts": rows])
        )
    }

    private func financial(_ profile: [String: Any]) throws -> NativeChatMCPFact? {
        try NativeChatMCPFact.make(
            tool: "get_my_financial_profile", option: nil,
            content: value(["success": true, "result": ["financial_profile": profile]])
        )
    }

    private static let owner = NativeChatMCPTurnOwner(
        accountId: "account-a", revision: 2, conversationId: "conversation-1", userMessageId: "turn-2"
    )

    private static func receipt(
        _ fact: NativeChatMCPFact,
        owner: NativeChatMCPTurnOwner = owner
    ) -> NativeChatMCPReceipt {
        .init(
            id: UUID(), callId: "call-1", owner: owner, toolName: "get_my_financial_profile",
            envelope: "synthetic envelope", kind: .response, fact: fact
        )
    }

    private func renderedTexts(_ fact: NativeChatMCPFact) throws -> [String] {
        var ledger = NativeChatMCPReceiptLedger(owner: Self.owner)
        XCTAssertTrue(ledger.record(Self.receipt(fact)))
        return try renderedTexts(ledger)
    }

    private func renderedTexts(_ ledger: NativeChatMCPReceiptLedger) throws -> [String] {
        let view = try NativeChatToolResultStatusView(ledger: ledger).inspect()
        return try view.findAll(ViewType.Text.self).map { try $0.string() }
    }
}

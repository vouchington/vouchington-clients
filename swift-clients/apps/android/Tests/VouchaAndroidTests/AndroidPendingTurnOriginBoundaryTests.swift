import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingTurnOriginBoundaryTests: XCTestCase {
    func testSameUserAtDifferentAPIOriginCannotRetryOrDiscardOtherOrigin() async throws {
        let firstFixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: firstFixture.host) }
        let secondFixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: secondFixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: firstFixture.recorder)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: secondFixture.recorder)
        let firstOwner = AndroidTranscriptTestFactory.pendingOwnerKey("same-user", client: firstFixture.client)
        let secondOwner = AndroidTranscriptTestFactory.pendingOwnerKey("same-user", client: secondFixture.client)
        XCTAssertNotEqual(firstOwner, secondOwner)
        let firstPending = makePending("first")
        let secondPending = makePending("second")
        let store = AndroidPendingTurnStoreTestDouble()
        store.records = [
            firstOwner: [firstPending.conversationID: firstPending],
            secondOwner: [secondPending.conversationID: secondPending]
        ]
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )

        await viewModel.bindAccount(client: firstFixture.client, userID: "same-user")
        XCTAssertEqual(viewModel.unlistedPendingTurns, [firstPending])
        await viewModel.bindAccount(client: secondFixture.client, userID: "same-user")
        XCTAssertEqual(viewModel.unlistedPendingTurns, [secondPending])
        let confirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())
        XCTAssertEqual(confirmation.ownerID, secondOwner)
        let discarded = await viewModel.discardPendingTurns(after: confirmation)
        XCTAssertTrue(discarded)
        XCTAssertEqual(store.discardCalls, [secondOwner])
        XCTAssertEqual(store.records[firstOwner]?[firstPending.conversationID], firstPending)
        XCTAssertNil(store.records[secondOwner])
        await viewModel.retryPendingTurn()
        XCTAssertTrue(secondFixture.recorder.requestSnapshot(
            path: "/api/v1/conversations/\(firstPending.conversationID)/client-generated-chat"
        ).isEmpty)

        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: firstFixture.recorder)
        await viewModel.bindAccount(client: firstFixture.client, userID: "same-user")
        XCTAssertEqual(viewModel.unlistedPendingTurns, [firstPending])
        XCTAssertTrue(firstFixture.recorder.requestSnapshot(
            path: "/api/v1/conversations/\(firstPending.conversationID)/client-generated-chat"
        ).isEmpty)
    }

    func testEquivalentAPIBaseNormalizesSchemeHostPortAndTrailingSlash() throws {
        let canonical = try XCTUnwrap(URL(string: "https://example.test:443/api"))
        let decorated = try XCTUnwrap(URL(string: "HTTPS://user:secret@EXAMPLE.test/api/?query=value#fragment"))
        let differentPath = try XCTUnwrap(URL(string: "https://example.test/api/other"))
        let differentScheme = try XCTUnwrap(URL(string: "http://example.test:443/api"))
        let owner = try XCTUnwrap(AndroidPendingTurnOwnerNamespace.key(userID: "same-user", baseURL: canonical))
        XCTAssertEqual(AndroidPendingTurnOwnerNamespace.key(userID: "same-user", baseURL: decorated), owner)
        XCTAssertNotEqual(AndroidPendingTurnOwnerNamespace.key(userID: "same-user", baseURL: differentPath), owner)
        XCTAssertNotEqual(AndroidPendingTurnOwnerNamespace.key(userID: "same-user", baseURL: differentScheme), owner)
        XCTAssertNotEqual(AndroidPendingTurnOwnerNamespace.key(userID: "other-user", baseURL: canonical), owner)
    }

    private func makePending(_ suffix: String) -> AndroidPendingChatTurn {
        AndroidPendingChatTurn(
            conversationID: "unlisted-\(suffix)",
            userMessageID: "user-\(suffix)", assistantMessageID: "assistant-\(suffix)",
            userText: "Question", assistantText: "Answer", modelProvider: "android_aicore", modelName: nil
        )
    }
}

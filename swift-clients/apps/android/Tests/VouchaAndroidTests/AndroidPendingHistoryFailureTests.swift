import Foundation
@testable import VouchaAndroid
import VouchaCore
import VouchaLocalization
import XCTest

@MainActor
final class AndroidPendingHistoryFailureTests: XCTestCase {
    func testFailedHistoryAfterReturningShowsExactPendingTurn() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        AndroidTranscriptTestFactory.enqueueConversationSwitchFlow(
            on: fixture.recorder, firstID: firstID, secondID: secondID
        )
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(firstID)/messages",
            statusCode: 503, data: Data("{}".utf8)
        )
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        let pending = AndroidPendingChatTurn(
            conversationID: firstID,
            userMessageID: "0198ffff-0001-7000-8000-000000000001",
            assistantMessageID: "0198ffff-0001-7000-8000-000000000002",
            userText: "Exact pending question", assistantText: "Exact pending answer",
            modelProvider: "android_aicore", modelName: "android-aicore-system"
        )
        viewModel.pendingTurns[firstID] = pending
        viewModel.pendingTurnErrors[firstID] = "Save unavailable"

        await viewModel.selectConversation(secondID)
        await viewModel.selectConversation(firstID)

        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        XCTAssertNotNil(viewModel.failedConversationHistoryLoad)
        XCTAssertEqual(viewModel.messages.map(\.id), [pending.userMessageID, pending.assistantMessageID])
        XCTAssertEqual(viewModel.messages.map(\.content), [pending.userText, pending.assistantText])
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToPersistOnDeviceResponse)
        XCTAssertTrue(fixture.recorder.requestSnapshot(
            path: "/api/v1/conversations/\(firstID)/client-generated-chat"
        ).isEmpty)
    }
}

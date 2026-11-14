import Foundation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import VouchaLocalization
import XCTest

@MainActor
final class AndroidPendingRetryRoundTripErrorTests: XCTestCase {
    func testPendingRetryFailureAppearsWhenReturningToOriginConversation() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        AndroidTranscriptTestFactory.enqueueConversationSwitchFlow(
            on: fixture.recorder,
            firstID: firstID,
            secondID: secondID
        )
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(firstID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        let retryPath = "/api/v1/conversations/\(firstID)/client-generated-chat"
        fixture.recorder.enqueue(path: retryPath, statusCode: 503, data: Data("{}".utf8), held: true)

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        let pending = AndroidPendingChatTurn(
            conversationID: firstID,
            userMessageID: "0198ffff-0001-7000-8000-000000000001",
            assistantMessageID: "0198ffff-0001-7000-8000-000000000002",
            userText: "Saved question",
            assistantText: "Saved answer",
            modelProvider: "android_aicore",
            modelName: "android-aicore-system"
        )
        viewModel.pendingTurns[firstID] = pending

        let retry = Task { await viewModel.retryPendingTurn() }
        _ = try await fixture.recorder.waitForRequest(path: retryPath, occurrence: 1)
        await viewModel.selectConversation(secondID)
        await viewModel.selectConversation(firstID)
        fixture.recorder.completeHeld(path: retryPath, occurrence: 1, statusCode: 503, data: Data("{}".utf8))
        await retry.value

        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        XCTAssertNotNil(viewModel.pendingTurnErrors[firstID])
        XCTAssertEqual(viewModel.errorMessage, viewModel.pendingTurnErrors[firstID])
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToPersistOnDeviceResponse)
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: retryPath).count, 1)
    }
}

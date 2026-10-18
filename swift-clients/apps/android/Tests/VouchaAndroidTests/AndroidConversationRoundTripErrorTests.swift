import Foundation
@testable import VouchaAndroid
import VouchaLocalization
import XCTest

@MainActor
final class AndroidConversationRoundTripErrorTests: XCTestCase {
    func testGenerationFailureAppearsWhenReturningToOriginConversation() async throws {
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
            data: AndroidTranscriptTestFactory.messagePageResponse(
                id: "returned-history",
                conversationID: firstID,
                content: "Returned history"
            )
        )

        let runtime = DelayedTranscriptRuntime()
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Original question"
        let send = Task { await viewModel.sendOnDevice() }
        try await runtime.waitUntilStarted()
        viewModel.draft = "Newer draft"
        await viewModel.selectConversation(secondID)
        await viewModel.selectConversation(firstID)

        await runtime.fail("generation unavailable")
        await send.value

        XCTAssertEqual(viewModel.selectedConversationID, firstID)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Returned history"])
        XCTAssertEqual(viewModel.draft, "Newer draft")
        let generationError = try XCTUnwrap(viewModel.generationErrors[firstID])
        XCTAssertEqual(viewModel.generationErrorForSelectedConversation, generationError)
        XCTAssertEqual(viewModel.errorMessage, generationError)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)
        XCTAssertEqual(viewModel.failedGenerationForSelectedConversation?.text, "Original question")
    }
}

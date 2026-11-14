import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidConversationCreationTests: XCTestCase {
    func testSendIsBlockedWhileNewConversationCreationKeepsOldConversationSelected() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let oldID = AndroidTranscriptTestFactory.newConversationID
        let newID = "0198ffff-0000-7000-8000-000000000003"
        let createPath = "/api/v1/conversations"
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        fixture.recorder.enqueue(
            path: createPath,
            data: AndroidTranscriptTestFactory.conversationResponse(id: newID, title: "New"),
            held: true
        )
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(newID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        let runtime = FakeAICoreRuntime(state: .available, response: "Should not generate")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")

        let creation = Task { await viewModel.createConversation() }
        _ = try await fixture.recorder.waitForRequest(path: createPath, occurrence: 1)
        viewModel.draft = "Question for new conversation"
        await viewModel.sendOnDevice()

        XCTAssertEqual(viewModel.selectedConversationID, oldID)
        XCTAssertTrue(viewModel.isCreatingConversation)
        XCTAssertEqual(viewModel.draft, "Question for new conversation")
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 0)
        XCTAssertTrue(fixture.recorder.requestSnapshot().allSatisfy {
            $0.url?.path != "/api/v1/conversations/\(oldID)/client-generated-chat"
        })

        fixture.recorder.completeHeld(
            path: createPath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.conversationResponse(id: newID, title: "New")
        )
        await creation.value
        XCTAssertEqual(viewModel.selectedConversationID, newID)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertEqual(viewModel.draft, "Question for new conversation")
    }
}

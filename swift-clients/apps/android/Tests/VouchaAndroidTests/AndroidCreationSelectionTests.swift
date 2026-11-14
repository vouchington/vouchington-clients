import Foundation
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidCreationSelectionTests: XCTestCase {
    func testSlowCreationSuccessOrFailurePreservesNewerSelectedConversation() async throws {
        for status in [200, 503] {
            let fixture = try AndroidTranscriptTestFactory.makeClient()
            defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
            let firstID = AndroidTranscriptTestFactory.newConversationID
            let secondID = "0198ffff-0000-7000-8000-000000000003"
            let createdID = "0198ffff-0000-7000-8000-000000000004"
            let path = "/api/v1/conversations"
            AndroidTranscriptTestFactory.enqueueConversationSwitchFlow(
                on: fixture.recorder, firstID: firstID, secondID: secondID
            )
            let response = AndroidTranscriptTestFactory.conversationResponse(id: createdID, title: "Created")
            fixture.recorder.enqueue(path: path, data: response, held: true)
            let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
            await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
            let creation = Task { await viewModel.createConversation() }
            _ = try await fixture.recorder.waitForRequest(path: path, occurrence: 1)
            await viewModel.selectConversation(secondID)
            viewModel.draft = "Draft for the selected conversation"
            fixture.recorder.completeHeld(path: path, occurrence: 1, statusCode: status, data: response)
            let selectedCreatedConversation = await creation.value
            XCTAssertFalse(selectedCreatedConversation)
            XCTAssertEqual(viewModel.selectedConversationID, secondID)
            XCTAssertEqual(viewModel.messages.first?.content, "Second conversation history")
            XCTAssertEqual(viewModel.draft, "Draft for the selected conversation")
            XCTAssertFalse(viewModel.conversationCreationFailed)
            XCTAssertNil(viewModel.errorMessage)
        }
    }

    func testAutoSendStopsWhenSelectionChangesDuringCreatedHistoryLoadEvenIfItReturns() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let createdID = AndroidTranscriptTestFactory.newConversationID
        let otherID = "0198ffff-0000-7000-8000-000000000003"
        let historyPath = "/api/v1/my/conversations/\(createdID)/messages"
        fixture.recorder.enqueue(path: "/api/v1/my/conversations", data: Data(
            #"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8
        ))
        fixture.recorder.enqueue(
            path: "/api/v1/conversations",
            data:
            AndroidTranscriptTestFactory.conversationResponse(id: createdID, title: "Created")
        )
        fixture.recorder.enqueue(
            path: historyPath,
            data:
            AndroidTranscriptTestFactory.emptyMessagePageResponse(),
            held: true
        )
        fixture.recorder.enqueue(path: historyPath, data: AndroidTranscriptTestFactory.emptyMessagePageResponse())
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(otherID)/messages",
            data:
            AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        let runtime = FakeAICoreRuntime(state: .available, response: "Must not generate")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Keep this question"
        let send = Task { await viewModel.sendOnDevice() }
        _ = try await fixture.recorder.waitForRequest(path: historyPath, occurrence: 1)
        await viewModel.selectConversation(otherID)
        await viewModel.selectConversation(createdID)
        fixture.recorder.completeHeld(
            path: historyPath,
            occurrence: 1,
            statusCode: 200,
            data:
            AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        await send.value
        XCTAssertEqual(viewModel.selectedConversationID, createdID)
        XCTAssertEqual(viewModel.draft, "Keep this question")
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 0)
        XCTAssertTrue(fixture.recorder.requestSnapshot().allSatisfy {
            $0.url?.path.hasSuffix("/client-generated-chat") != true
        })
    }

    func testAutoSendPreservesDraftWhenCreatedHistoryFailsToLoad() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let createdID = AndroidTranscriptTestFactory.newConversationID
        fixture.recorder.enqueue(path: "/api/v1/my/conversations", data: Data(
            #"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8
        ))
        fixture.recorder.enqueue(
            path: "/api/v1/conversations",
            data:
            AndroidTranscriptTestFactory.conversationResponse(id: createdID, title: "Created")
        )
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(createdID)/messages",
            statusCode: 503,
            data: Data(#"{"message":"offline"}"#.utf8)
        )
        let runtime = FakeAICoreRuntime(state: .available, response: "Must not generate")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Keep this question"
        await viewModel.sendOnDevice()
        XCTAssertTrue(viewModel.transcriptLoadFailed)
        XCTAssertEqual(viewModel.draft, "Keep this question")
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 0)
    }

}

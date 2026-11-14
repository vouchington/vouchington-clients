import Foundation
@testable import VouchaAndroid
import VouchaLocalization
import XCTest

@MainActor
final class AndroidGenerationErrorOwnershipTests: XCTestCase {
    func testDraftBackedGenerationErrorBelongsToItsConversationAndAccount() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000002"
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.twoConversationListResponse(firstID: firstID, secondID: secondID)
        )
        enqueueHistory(firstID, on: fixture.recorder)
        let runtime = DelayedTranscriptRuntime()
        let model = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        model.modelState = .available
        await model.bindAccount(client: fixture.client, userID: "fixture-user")
        model.draft = "Keep this draft"
        let send = Task { await model.sendOnDevice() }
        try await runtime.waitUntilStarted()
        await runtime.fail("generation unavailable")
        await send.value
        let failure = try XCTUnwrap(model.errorMessage)
        XCTAssertNil(model.failedGenerationForSelectedConversation)

        enqueueHistory(secondID, on: fixture.recorder)
        await model.selectConversation(secondID)

        XCTAssertNil(model.errorMessage)
        XCTAssertNil(model.chatErrorMessageKey)
        XCTAssertNil(model.generationErrorForSelectedConversation)
        enqueueHistory(firstID, on: fixture.recorder)
        await model.selectConversation(firstID)
        XCTAssertEqual(model.errorMessage, failure)
        XCTAssertEqual(model.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)

        await model.bindAccount(client: nil, userID: nil)
        XCTAssertTrue(model.generationErrors.isEmpty)
        XCTAssertNil(model.errorMessage)
    }

    private func enqueueHistory(_ id: String, on recorder: AndroidTranscriptRecorder) {
        recorder.enqueue(
            path: "/api/v1/my/conversations/\(id)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
    }
}

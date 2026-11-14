import Foundation
@testable import VouchaAndroid
import VouchaLocalization
import XCTest

@MainActor
final class AndroidInterruptedHistoryTests: XCTestCase {
    func testCanonicalInterruptedHistoryPreservesVisibleFailure() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        try fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        try fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(conversationID)/messages",
            data: AndroidTranscriptFixtures.data("native.chat.incomplete")
        )
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))

        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")

        let message = try XCTUnwrap(viewModel.messages.first)
        XCTAssertEqual(message.role, "assistant")
        XCTAssertEqual(message.content, "")
        XCTAssertEqual(message.error, .message(.nativeSwiftChatResponseInterrupted))
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertFalse(viewModel.transcriptLoadFailed)
    }
}

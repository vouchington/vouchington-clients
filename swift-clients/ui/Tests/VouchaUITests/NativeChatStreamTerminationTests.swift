import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeChatStreamTerminationTests: XCTestCase {
    func testIncompleteHostedStreamUsesLocalizedRetryMessage() async {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")
        let events = AsyncThrowingStream<ChatStreamEvent, Error> { continuation in
            continuation.finish(throwing: ChatSSEReaderError.incompleteStream)
        }

        await viewModel.streamHostedDraftMessage(
            events: events,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false
            )
        )

        XCTAssertEqual(
            viewModel.streamErrorMessage,
            .message(.nativeSwiftChatResponseInterrupted)
        )
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testNamedErrorRemainsTerminalWithoutFinishingAsSuccess() async {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")
        let events = AsyncStream<ChatStreamEvent> { continuation in
            continuation.yield(.error("Hosted failed"))
            continuation.finish()
        }

        await viewModel.streamHostedDraftMessage(
            events: events,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false
            )
        )

        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("Hosted failed"))
        XCTAssertFalse(viewModel.isStreaming)
    }
}

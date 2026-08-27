import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatViewModelStreamingTests: XCTestCase {
    func testChatViewModelKeepsOptimisticUserMessageDuringSuccessfulStream() {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        let userMessageId = "local-user-1"
        viewModel.messages = [
            .init(id: userMessageId, role: .user, content: "Hello support", isStreaming: false)
        ]

        viewModel.beginStreaming(conversationId: "conversation-3", userMessageId: userMessageId)
        viewModel.apply(
            event: .metadata(
                .init(
                    conversationId: "conversation-3",
                    userMessageId: "user-message-1",
                    assistantMessageId: "assistant-message-1",
                    jobId: "job-1"
                )
            )
        )
        viewModel.apply(event: .text("Hello back"))
        viewModel.apply(event: .done)

        XCTAssertEqual(viewModel.messages.count, 2)
        XCTAssertEqual(viewModel.messages.first?.role, .user)
        XCTAssertEqual(viewModel.messages.first?.content, "Hello support")
        XCTAssertEqual(viewModel.messages.last?.role, .assistant)
        XCTAssertEqual(viewModel.messages.last?.content, "Hello back")
        XCTAssertNil(viewModel.streamErrorMessage)
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testChatViewModelStopsAssistantBubbleStreamingStateWhenAborted() {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        let userMessageId = "local-user-1"
        viewModel.messages = [
            .init(id: userMessageId, role: .user, content: "Hello support", isStreaming: false)
        ]

        viewModel.beginStreaming(conversationId: "conversation-3", userMessageId: userMessageId)
        viewModel.apply(
            event: .metadata(
                .init(
                    conversationId: "conversation-3",
                    userMessageId: "user-message-1",
                    assistantMessageId: "assistant-message-1",
                    jobId: "job-1"
                )
            )
        )
        viewModel.abortStreaming()

        XCTAssertEqual(viewModel.messages.count, 2)
        XCTAssertFalse(viewModel.messages.last?.isStreaming ?? true)
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertNil(viewModel.streamingConversationId)
        XCTAssertNil(viewModel.streamingUserMessageId)
        XCTAssertNil(viewModel.streamingAssistantMessageId)
    }

    func testChatViewModelClearsStreamErrorWhenSelectionChangesOrResets() async {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.selectedConversationId = "conversation-1"
        viewModel.streamErrorMessage = .verbatim("Stream failed")

        await viewModel.selectConversation(id: "conversation-2")
        XCTAssertNil(viewModel.streamErrorMessage)

        viewModel.streamErrorMessage = .verbatim("Stream failed")
        await viewModel.selectConversation(id: nil)

        XCTAssertNil(viewModel.streamErrorMessage)
    }

}

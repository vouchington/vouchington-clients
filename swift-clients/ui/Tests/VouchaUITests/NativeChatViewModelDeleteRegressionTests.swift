import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatViewModelDeleteRegressionTests: NativeRouteSurfaceViewModelTestCase {
    func testChatViewModelDoesNotClearTitleDraftWhenDeleteFinishesAfterSelectionChanges() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations/conversation-1"] = [
            (Data("{}".utf8), 204, 0.1)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-2/messages"] = (
            Self.chatFreshConversationTwoDetailData,
            200
        )

        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.conversations = try [
            makeConversation(
                id: "conversation-1",
                title: "First chat",
                createdAt: "2026-01-01T00:00:00Z",
                updatedAt: "2026-01-01T00:01:00Z"
            ),
            makeConversation(
                id: "conversation-2",
                title: "Second chat",
                createdAt: "2026-01-01T00:02:00Z",
                updatedAt: "2026-01-01T00:03:00Z"
            )
        ]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversationTitleDraft = "First chat"

        let delete = Task { await viewModel.deleteSelectedConversation() }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.selectConversation(id: "conversation-2")
        await delete.value

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.conversationTitleDraft, "Second chat")
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-2"])
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-fresh"])
    }
}

private extension NativeChatViewModelDeleteRegressionTests {
    func makeConversation(
        id: String,
        title: String,
        createdAt: String,
        updatedAt: String
    ) throws -> ChatConversation {
        try NativeChatTestFixtures.decode(
            ChatConversation.self,
            """
            {
              "id": "\(id)",
              "title": "\(title)",
              "created_at": "\(createdAt)",
              "created_by_id": "user-1",
              "updated_at": "\(updatedAt)",
              "updated_by_id": null,
              "deleted_at": null,
              "deleted_by_id": null
            }
            """
        )
    }

    static let chatFreshConversationTwoDetailData = Data(
        """
        {
          "results": [
            {
              "id": "message-fresh",
              "conversation_id": "conversation-2",
              "created_at": "2026-01-01T00:05:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-01-01T00:05:00Z",
              "updated_by_id": null,
              "deleted_at": null,
              "deleted_by_id": null,
              "content": {
                "role": "assistant",
                "content": "Fresh reply",
                "error": null
              }
            }
          ],
          "page_info": {
            "has_next_page": false,
            "start_cursor": "message-fresh",
            "end_cursor": null
          }
        }
        """.utf8
    )
}

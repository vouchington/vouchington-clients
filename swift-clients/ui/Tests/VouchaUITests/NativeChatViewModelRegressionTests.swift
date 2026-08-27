import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatViewModelRegressionTests: NativeRouteSurfaceViewModelTestCase {
    func testChatViewModelIgnoresStaleConversationDetailResponsesWhenSelectionChanges() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations/conversation-1/messages"] = [(
            Self.chatStaleConversationOneDetailData,
            200,
            0.1
        )]
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

        let staleSelection = Task { await viewModel.selectConversation(id: "conversation-1") }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.selectConversation(id: "conversation-2")
        await staleSelection.value

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.conversationTitleDraft, "Second chat")
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-fresh"])
        XCTAssertEqual(viewModel.messages.first?.content, "Fresh reply")
    }

    func testChatViewModelSkipsConcurrentConversationPaginationRequests() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations"] = [
            (NativeChatSupportSurfaceTests.chatConversationListPageTwoData, 200, 0.1),
            (NativeChatSupportSurfaceTests.chatConversationListPageTwoData, 200, 0.1)
        ]

        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.conversations = try [
            makeConversation(
                id: "conversation-1",
                title: "First chat",
                createdAt: "2026-01-01T00:00:00Z",
                updatedAt: "2026-01-01T00:01:00Z"
            )
        ]
        viewModel.listPageInfo = .init(hasNextPage: true, endCursor: "cursor-1")

        let first = Task { await viewModel.loadMoreConversations() }
        let second = Task { await viewModel.loadMoreConversations() }
        await first.value
        await second.value

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1", "conversation-2"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/conversations" }.count,
            1
        )
    }

    func testChatViewModelSkipsCreateResultWhenSelectionChangesBeforeSendStarts() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/conversations"] = [
            (NativeChatSupportSurfaceTests.createdConversationData, 201, 0.1)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1/messages"] = (
            NativeChatSupportSurfaceTests.chatMessagesData,
            200
        )

        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.conversations = try [
            makeConversation(
                id: "conversation-1",
                title: "Existing chat",
                createdAt: "2026-01-01T00:00:00Z",
                updatedAt: "2026-01-01T00:01:00Z"
            )
        ]
        viewModel.draftMessage = "Hello"

        let send = Task { await viewModel.sendDraftMessage() }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.selectConversation(id: "conversation-1")
        await send.value

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-1")
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])
        XCTAssertEqual(viewModel.messages.first?.content, "Hello")
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertNil(viewModel.detailErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/conversations",
            "/api/v1/my/conversations/conversation-1/messages"
        ])
    }

    func testChatViewModelKeepsStreamWhenReselectingCurrentConversation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1/messages"] = (
            NativeChatSupportSurfaceTests.chatMessagesData,
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
            )
        ]

        await viewModel.selectConversation(id: "conversation-1")
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "message-user")

        await viewModel.selectConversation(id: "conversation-1")

        XCTAssertTrue(viewModel.isStreaming)
        XCTAssertEqual(viewModel.streamingConversationId, "conversation-1")
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs
                .filter { $0.path == "/api/v1/my/conversations/conversation-1/messages" }
                .count,
            1
        )
    }

    func testChatViewModelKeepsExistingConversationMessageWhenStreamFailsBeforeMetadata() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-1/chat"] = (
            NativeChatSupportSurfaceTests.errorData,
            500
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.titleProviderSelection = .openAI
        viewModel.selectedConversationId = "conversation-1"
        viewModel.draftMessage = "Hello"

        await viewModel.sendDraftMessage()

        XCTAssertEqual(viewModel.messages.map(\.content), ["Hello"])
        XCTAssertNotNil(viewModel.streamErrorMessage)
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testChatViewModelDoesNotOverwriteDraftWhenRenameFinishesAfterSelectionChanges() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations/conversation-1"] = [
            (NativeChatSupportSurfaceTests.renamedConversationData, 200, 0.1)
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
        viewModel.conversationTitleDraft = "Renamed chat"

        let rename = Task { await viewModel.renameSelectedConversation() }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.selectConversation(id: "conversation-2")
        await rename.value

        XCTAssertEqual(viewModel.conversations.first { $0.id == "conversation-1" }?.title, "Renamed chat")
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.conversationTitleDraft, "Second chat")
    }

    func testChatViewModelDeepLinkHydratesMissingConversationMetadataFromListResult() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations"] = (Self.chatDeepLinkedConversationListData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-9/messages"] = (
            Self.chatDeepLinkedConversationDetailData,
            200
        )

        let client = try makeClient()
        let viewModel = NativeChatViewModel(
            client: client,
            routeMatch: NativeRouteMatch(
                path: "/app/chat/conversation-9",
                template: "/app/chat/:id",
                params: ["id": "conversation-9"]
            )
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-9")
        XCTAssertEqual(viewModel.selectedConversationTitle, .verbatim("Deep linked title"))
        XCTAssertEqual(viewModel.conversationTitleDraft, "Deep linked title")
        XCTAssertEqual(viewModel.conversations.first?.id, "conversation-9")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "GET"])
    }

    func testChatViewModelPreservesSelectedConversationDuringResetRefresh() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations"] = (Self.chatMissingDeepLinkListData, 200)

        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        let localConversation = try makeConversation(
            id: "conversation-local",
            title: "Local draft",
            createdAt: "2026-01-01T00:10:00Z",
            updatedAt: "2026-01-01T00:11:00Z"
        )
        viewModel.conversations = [localConversation]
        viewModel.selectedConversationId = "conversation-local"
        viewModel.createdConversationIds.insert("conversation-local")

        await viewModel.loadConversations(reset: true)

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1", "conversation-local"])
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-local")
    }

    func testChatViewModelSkipsTitleGenerationAfterAbortedStream() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/conversations"] = (
            NativeChatSupportSurfaceTests.createdConversationData,
            201
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/conversations/conversation-3/chat"] = [
            (
                Data(
                    """
                    event: done
                    data: {}

                    """.utf8
                ),
                200,
                0.2
            )
        ]

        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.draftMessage = "Hello"

        let send = Task { await viewModel.sendDraftMessage() }
        try await Task.sleep(nanoseconds: 20_000_000)
        viewModel.abortStreaming()
        await send.value

        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertNil(viewModel.streamingConversationId)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.map(\.path)
            .contains("/api/v1/my/conversations/conversation-3/title"))
    }

    func testChatViewModelGeneratesTitleAfterSuccessfulCreatedStreamWithDoneEvent() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-3/title"] = (
            Self.generatedConversationThreeData,
            200
        )

        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.titleProviderSelection = .openAI
        viewModel.conversations = try [
            makeConversation(
                id: "conversation-3",
                title: "",
                createdAt: "2026-01-01T00:07:00Z",
                updatedAt: "2026-01-01T00:07:00Z"
            )
        ]
        viewModel.selectedConversationId = "conversation-3"
        viewModel.createdConversationIds.insert("conversation-3")
        viewModel.beginStreaming(conversationId: "conversation-3", userMessageId: "local-user-1")
        viewModel.apply(event: .done)

        await viewModel.finishStream(
            conversationId: "conversation-3",
            userMessageId: "local-user-1",
            createdConversation: true
        )

        XCTAssertEqual(viewModel.conversationTitleDraft, "Generated chat")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.map(\.path)
            .contains("/api/v1/my/conversations/conversation-3/title"))
    }

}

private extension NativeChatViewModelRegressionTests {
    func makeConversation(
        id: String,
        title: String,
        createdAt: String,
        updatedAt: String
    ) throws -> ChatConversation {
        try NativeChatSupportSurfaceTests.decode(
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

    static let chatStaleConversationOneDetailData = Data(
        """
        {
          "results": [
            {
              "id": "message-stale",
              "conversation_id": "conversation-1",
              "created_at": "2026-01-01T00:04:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-01-01T00:04:00Z",
              "updated_by_id": null,
              "deleted_at": null,
              "deleted_by_id": null,
              "content": {
                "role": "assistant",
                "content": "Stale reply",
                "error": null
              }
            }
          ],
          "page_info": {
            "has_next_page": false,
            "start_cursor": "message-stale",
            "end_cursor": null
          }
        }
        """.utf8
    )

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

    static let chatMissingDeepLinkListData = Data(
        """
        {
          "results": [
            {
              "id": "conversation-1",
              "title": "First chat",
              "created_at": "2026-01-01T00:00:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-01-01T00:01:00Z",
              "updated_by_id": null,
              "deleted_at": null,
              "deleted_by_id": null
            }
          ],
          "page_info": {
            "has_next_page": false,
            "start_cursor": "conversation-1",
            "end_cursor": null
          }
        }
        """.utf8
    )

    static let chatDeepLinkedConversationListData = Data(
        """
        {
          "results": [
            {
              "id": "conversation-9",
              "title": "Deep linked title",
              "created_at": "2026-01-01T00:00:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-01-01T00:07:00Z",
              "updated_by_id": "user-1",
              "deleted_at": null,
              "deleted_by_id": null
            }
          ],
          "page_info": {
            "has_next_page": false,
            "start_cursor": "conversation-9",
            "end_cursor": null
          }
        }
        """.utf8
    )

    static let chatDeepLinkedConversationDetailData = Data(
        """
        {
          "results": [
            {
              "id": "message-deep",
              "conversation_id": "conversation-9",
              "created_at": "2026-01-01T00:06:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-01-01T00:06:00Z",
              "updated_by_id": null,
              "deleted_at": null,
              "deleted_by_id": null,
              "content": {
                "role": "assistant",
                "content": "Deep reply",
                "error": null
              }
            }
          ],
          "page_info": {
            "has_next_page": false,
            "start_cursor": "message-deep",
            "end_cursor": null
          }
        }
        """.utf8
    )

    static let generatedConversationThreeData = Data(
        """
        {
          "conversation": {
            "id": "conversation-3",
            "title": "Generated chat",
            "created_at": "2026-01-01T00:07:00Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:08:00Z",
            "updated_by_id": "user-1",
            "deleted_at": null,
            "deleted_by_id": null
          }
        }
        """.utf8
    )

}

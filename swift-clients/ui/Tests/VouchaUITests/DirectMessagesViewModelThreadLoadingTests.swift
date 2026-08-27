import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelThreadLoadingTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testLoadMoreMessagesIsIgnoredWhileInitialThreadPageIsLoading() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPage(conversationId: "conversation-1", ownerUserId: "user-1"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                messagesPage(
                    conversationId: "conversation-1",
                    ids: ["message-1"],
                    endCursor: "message-cursor-1",
                    hasNextPage: true
                ),
                200,
                0.2
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let loadThread = Task {
            await viewModel.loadThread(conversationId: "conversation-1")
        }
        try await waitForCapturedPath(
            "/api/v1/my/messages/conversation-1/messages",
            count: 1
        )

        await viewModel.loadMoreMessages()
        _ = await loadThread.value

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/messages/conversation-1/messages" }
                .count,
            1
        )
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-1"])
        assertLoadState(viewModel.threadState, .loaded)
    }

    func testSendMessageIsIgnoredWhileInitialThreadPageIsLoading() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPage(conversationId: "conversation-1", ownerUserId: "user-1"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                messagesPage(conversationId: "conversation-1", ids: ["message-1"]),
                200,
                0.2
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let loadThread = Task {
            await viewModel.loadThread(conversationId: "conversation-1")
        }
        try await waitForCapturedPath(
            "/api/v1/my/messages/conversation-1/messages",
            count: 1
        )

        let didSend = await viewModel.sendMessage(text: "Reply during load")
        _ = await loadThread.value

        XCTAssertFalse(didSend)
        XCTAssertFalse(CannedFeedURLProtocol.capturedMethods.contains("POST"))
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-1"])
        assertLoadState(viewModel.threadState, .loaded)
    }

    func testFailedOlderMessageLoadDoesNotPoisonThreadState() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPage(conversationId: "conversation-1", ownerUserId: "user-1"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                messagesPage(
                    conversationId: "conversation-1",
                    ids: ["message-1"],
                    endCursor: "message-cursor-1",
                    hasNextPage: true
                ),
                200,
                0
            ),
            (Data("{}".utf8), 500, 0),
            (
                messagesPage(
                    conversationId: "conversation-1",
                    ids: ["message-2"],
                    endCursor: nil,
                    hasNextPage: false
                ),
                200,
                0
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadThread(conversationId: "conversation-1")
        await viewModel.loadMoreMessages()
        assertLoadState(viewModel.threadState, .loaded)

        await viewModel.loadMoreMessages()
        assertLoadState(viewModel.threadState, .loaded)
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-2", "message-1"])
    }

    func testOlderMessagesDeduplicateOverlapsAndPreserveCurrentMessage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPage(conversationId: "conversation-1", ownerUserId: "user-1"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                messagesPage(
                    conversationId: "conversation-1",
                    items: [("message-2", "Current")],
                    endCursor: "cursor-1",
                    hasNextPage: true
                ),
                200,
                0
            ),
            (
                messagesPage(conversationId: "conversation-1", items: [
                    ("message-1", "Older"),
                    ("message-1", "Duplicate older"),
                    ("message-2", "Stale current")
                ]),
                200,
                0
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadThread(conversationId: "conversation-1")
        await viewModel.loadMoreMessages()

        XCTAssertEqual(viewModel.messages.map(\.id), ["message-1", "message-2"])
        XCTAssertEqual(viewModel.messages.map(\.bodyText), ["Older", "Current"])
    }

    func testInitialMessageLoadDoesNotClearThreadErrorFromParallelChildRequests() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            Data("{}".utf8),
            500
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                messagesPage(
                    conversationId: "conversation-1",
                    ids: ["message-1"],
                    endCursor: nil,
                    hasNextPage: false
                ),
                200,
                0.2
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadThread(conversationId: "conversation-1")

        assertLoadState(viewModel.threadState, .error(.api(statusCode: 500, preconditionCode: nil)))
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-1"])
    }

    func testSendMessageClearsThreadErrorOnSuccessfulSend() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages"] = (
            Data(
                """
                {
                  "results": [],
                  "page_info": {
                    "has_next_page": false,
                    "end_cursor": null,
                    "start_cursor": null
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (
            directMessageResponseData(
                conversationId: "conversation-1",
                id: "message-1",
                bodyText: "Reply from native"
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.threadState = .error(.api(statusCode: 500, preconditionCode: nil))

        let didSend = await viewModel.sendMessage(text: "Reply from native")

        XCTAssertTrue(didSend)
        assertLoadState(viewModel.threadState, .loaded)
        XCTAssertEqual(viewModel.messages.map(\.bodyText), ["Reply from native"])
    }

    private func directConversationResponseData(
        participantAddPolicy: ConversationParticipantAddPolicy
    ) -> Data {
        Data(
            """
            {
              "conversation": {
                "id": "conversation-1",
                "channel_type": "direct",
                "title": "Support follow-up",
                "created_at": "2026-01-01T00:00:00Z",
                "created_by_id": "user-1",
                "updated_at": "2026-01-01T00:00:00Z",
                "participant_usernames": ["alice", "bob"],
                "participant_add_policy": "\(participantAddPolicy.rawValue)"
              }
            }
            """.utf8
        )
    }

    private func messagesPage(
        conversationId: String,
        ids: [String],
        endCursor: String? = nil,
        hasNextPage: Bool = false
    ) -> Data {
        messagesPage(
            conversationId: conversationId,
            items: ids.map { ($0, "Hello native") },
            endCursor: endCursor,
            hasNextPage: hasNextPage
        )
    }

    private func messagesPage(
        conversationId: String,
        items: [(id: String, bodyText: String)],
        endCursor: String? = nil,
        hasNextPage: Bool = false
    ) -> Data {
        Data(
            """
            {
              "results": [
                \(items.map { messageJSON(conversationId: conversationId, id: $0.id, bodyText: $0.bodyText) }
                .joined(separator: ","))
              ],
              "page_info": {
                "has_next_page": \(hasNextPage),
                "end_cursor": \(jsonString(endCursor)),
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func participantsPage(conversationId: String, ownerUserId: String) -> Data {
        Data(
            """
            {
              "results": [
                {
                  "id": "participant-owner",
                  "conversation_id": "\(conversationId)",
                  "user_id": "\(ownerUserId)",
                  "role": "owner",
                  "created_at": "2026-01-01T00:00:00Z",
                  "removed_at": null,
                  "username": "alice",
                  "profile_image_id": null
                }
              ],
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func messageJSON(conversationId: String, id: String, bodyText: String) -> String {
        """
        {
          "id": "\(id)",
          "conversation_id": "\(conversationId)",
          "body_text": "\(bodyText)",
          "created_at": "2026-01-01T00:00:00Z",
          "updated_at": "2026-01-01T00:00:00Z",
          "deleted_at": null,
          "created_by_id": "user-2",
          "sender_username": "bob"
        }
        """
    }

    private func directMessageResponseData(
        conversationId: String,
        id: String,
        bodyText: String
    ) -> Data {
        Data(
            """
            {
              "message": {
                "id": "\(id)",
                "conversation_id": "\(conversationId)",
                "body_text": "\(bodyText)",
                "created_by_id": "user-1",
                "sender_username": "alice",
                "created_at": "2026-01-01T00:00:00Z",
                "updated_at": "2026-01-01T00:00:00Z",
                "deleted_at": null
              }
            }
            """.utf8
        )
    }

    private func jsonString(_ value: String?) -> String {
        guard let value else { return "null" }
        return "\"\(value)\""
    }

    private func assertLoadState(
        _ state: LoadState,
        _ expected: LoadState,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        switch (state, expected) {
        case (.idle, .idle), (.loading, .loading), (.loaded, .loaded):
            return
        case let (.error(lhs), .error(rhs)):
            XCTAssertEqual(lhs.localizedDescription, rhs.localizedDescription, file: file, line: line)
        default:
            XCTFail("Unexpected state mismatch", file: file, line: line)
        }
    }

    private func waitForCapturedPath(
        _ path: String,
        count: Int,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async throws {
        for _ in 0 ..< 40 {
            if CannedFeedURLProtocol.capturedURLs.filter({ $0.path == path }).count >= count {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for captured path \(path)", file: file, line: line)
    }
}

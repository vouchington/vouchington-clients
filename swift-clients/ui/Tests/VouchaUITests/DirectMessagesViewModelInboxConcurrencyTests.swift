import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelInboxConcurrencyTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testConcurrentInboxLoadsDoNotDuplicateRequestsOrPages() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationPageData(id: "conversation-1", endCursor: "cursor-1", hasNextPage: true), 200, 0.2),
            (conversationPageData(id: "conversation-2", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let firstLoad = Task { await viewModel.loadMoreConversations() }
        try await Task.sleep(nanoseconds: 50_000_000)
        let secondLoad = Task { await viewModel.loadMoreConversations() }
        _ = await firstLoad.value
        _ = await secondLoad.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/messages" }.count, 1)
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])
        XCTAssertTrue(viewModel.hasMoreConversations)
        assertLoadState(viewModel.state, .loaded)
    }

    func testInboxContinuationDeduplicatesOverlapsAndPreservesCurrentConversation() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (
                conversationPageData(
                    items: [("conversation-1", "Current title")],
                    endCursor: "cursor-1",
                    hasNextPage: true
                ),
                200,
                0
            ),
            (
                conversationPageData(
                    items: [
                        ("conversation-2", "Older title"),
                        ("conversation-2", "Duplicate older title"),
                        ("conversation-1", "Stale current title")
                    ],
                    endCursor: nil,
                    hasNextPage: false
                ),
                200,
                0
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadInbox()
        await viewModel.loadMoreConversations()

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1", "conversation-2"])
        XCTAssertEqual(viewModel.conversations.first?.title, "Current title")
        XCTAssertEqual(viewModel.conversations.last?.title, "Older title")
    }

    func testReloadInboxDoesNotEmptyRowsDuringInFlightLoadMore() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationPageData(id: "conversation-1", endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (conversationPageData(id: "conversation-older", endCursor: nil, hasNextPage: false), 200, 0.2),
            (conversationPageData(id: "conversation-newest", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadInbox()
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])

        let loadMore = Task { await viewModel.loadMoreConversations() }
        try await waitForCapturedPath("/api/v1/my/messages", count: 2)
        await viewModel.reloadInbox()

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])
        _ = await loadMore.value

        for _ in 0 ..< 40 {
            if viewModel.conversations.map(\.id) == ["conversation-newest"] {
                break
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-newest"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/messages" }.count, 3)
    }

    func testReloadInboxWaitsForInFlightPageBeforeReplacingRows() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationPageData(id: "conversation-1", endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (conversationPageData(id: "older-conversation", endCursor: nil, hasNextPage: false), 200, 0.2),
            (conversationPageData(id: "newest-conversation", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadInbox()
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])

        let olderPage = Task { await viewModel.loadMoreConversations() }
        try await waitForCapturedPath("/api/v1/my/messages", count: 2)
        await viewModel.reloadInbox()
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])
        await olderPage.value

        for _ in 0 ..< 40 {
            if viewModel.conversations.map(\.id) == ["newest-conversation"] {
                break
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }

        XCTAssertEqual(viewModel.conversations.map(\.id), ["newest-conversation"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/messages" }.count, 3)
    }

    func testSendMessageIgnoresStaleCompletionAfterConversationSwitch() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                directMessageResponseData(conversationId: "conversation-1", id: "message-1", bodyText: "Stale reply"),
                200,
                0.2
            )
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationPageData(id: "conversation-1", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.messages = [message(id: "message-b", conversationId: "conversation-2", bodyText: "Thread B")]

        let sendTask = Task { await viewModel.sendMessage(text: "  Stale reply  ") }
        try await waitForCapturedPath("/api/v1/my/messages/conversation-1/messages", count: 1)
        viewModel.selectedConversationId = "conversation-2"
        viewModel.messages = [message(id: "message-b", conversationId: "conversation-2", bodyText: "Thread B")]

        let didSend = await sendTask.value

        XCTAssertTrue(didSend)
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-b"])
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/messages" }.count, 1)
        assertLoadState(viewModel.threadState, .idle)
    }

    func testSendMessageRefreshesInboxAfterSelectionReturnsToNil() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                directMessageResponseData(
                    conversationId: "conversation-1",
                    id: "message-1",
                    bodyText: "Reply from inbox"
                ),
                200,
                0.2
            )
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationPageData(id: "conversation-1", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.messages = [message(id: "message-b", conversationId: "conversation-1", bodyText: "Thread B")]

        let sendTask = Task { await viewModel.sendMessage(text: "  Reply from inbox  ") }
        try await waitForCapturedPath("/api/v1/my/messages/conversation-1/messages", count: 1)
        viewModel.selectedConversationId = nil
        viewModel.messages = [message(id: "message-b", conversationId: "conversation-1", bodyText: "Thread B")]

        let didSend = await sendTask.value

        XCTAssertTrue(didSend)
        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-b"])
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/messages" }.count, 1)
        assertLoadState(viewModel.threadState, .idle)
    }

    func testSendMessageAppendsReplyWhenOptimisticRowWasClearedByReload() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                directMessageResponseData(
                    conversationId: "conversation-1",
                    id: "message-1",
                    bodyText: "Reply from native"
                ),
                200,
                0.2
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.messages = [message(id: "message-previous", conversationId: "conversation-1", bodyText: "Earlier")]

        let sendTask = Task { await viewModel.sendMessage(text: "  Reply from native  ") }
        try await waitForCapturedPath("/api/v1/my/messages/conversation-1/messages", count: 1)
        viewModel.messages = []

        let didSend = await sendTask.value

        XCTAssertTrue(didSend)
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-1"])
        XCTAssertEqual(viewModel.messages.last?.bodyText, "Reply from native")
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-1")
    }

    func testDoubleTapConversationCreationOnlySendsOneCreateRequest() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (
                Data(
                    """
                    {
                      "conversation": {
                        "id": "conversation-99",
                        "channel_type": null,
                        "title": "",
                        "created_at": "2026-01-01T00:00:00Z",
                        "created_by_id": "user-1",
                        "updated_at": "2026-01-01T00:00:00Z",
                        "participant_usernames": null,
                        "participant_add_policy": "all_members"
                      }
                    }
                    """.utf8
                ),
                200,
                0.2
            ),
            (conversationPageData(id: "conversation-99", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (
                directMessageResponseData(
                    conversationId: "conversation-99",
                    id: "message-99",
                    bodyText: "Hello native"
                ),
                200,
                0
            ),
            (
                Data(
                    """
                    {
                      "results": [
                        {
                          "id": "message-99",
                          "conversation_id": "conversation-99",
                          "body_text": "Hello native",
                          "created_by_id": "user-1",
                          "sender_username": "alice",
                          "created_at": "2026-01-01T00:00:00Z",
                          "updated_at": "2026-01-01T00:00:00Z",
                          "deleted_at": null
                        }
                      ],
                      "page_info": {
                        "has_next_page": false,
                        "end_cursor": null,
                        "start_cursor": null
                      }
                    }
                    """.utf8
                ),
                200,
                0
            )
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            participantsPage(conversationId: "conversation-99", ownerUserId: "user-1"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99"] = (
            directConversationResponseData(id: "conversation-99", participantAddPolicy: .allMembers),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let firstCreate = Task {
            await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")
        }
        try await waitForCapturedPath("/api/v1/my/messages", count: 1)
        let secondCreate = Task {
            await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")
        }
        let firstResult = await firstCreate.value
        let secondResult = await secondCreate.value

        let createRequestCount = zip(CannedFeedURLProtocol.capturedMethods, CannedFeedURLProtocol.capturedURLs)
            .filter { method, url in
                method == "POST" && url.path == "/api/v1/my/messages"
            }
            .count
        XCTAssertEqual(firstResult, .created)
        XCTAssertEqual(secondResult, .ignored)
        XCTAssertEqual(createRequestCount, 1)
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-99")
        XCTAssertEqual(viewModel.conversations.first?.id, "conversation-99")
    }

    private func conversationPageData(id: String, endCursor: String?, hasNextPage: Bool) -> Data {
        conversationPageData(items: [(id, "Support follow-up")], endCursor: endCursor, hasNextPage: hasNextPage)
    }

    private func conversationPageData(
        items: [(id: String, title: String)],
        endCursor: String?,
        hasNextPage: Bool
    ) -> Data {
        let results = items.map { item in
            """
            {
              "id": "\(item.id)",
              "channel_type": null,
              "title": "\(item.title)",
              "created_at": "2026-01-01T00:00:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-01-01T00:00:00Z",
              "participant_usernames": ["alice", "bob"],
              "participant_add_policy": "all_members"
            }
            """
        }.joined(separator: ",")
        return Data(
            """
            {
              "results": [
                \(results)
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

    private func directConversationResponseData(
        id: String,
        participantAddPolicy: ConversationParticipantAddPolicy
    ) -> Data {
        Data(
            """
            {
              "conversation": {
                "id": "\(id)",
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

    private func directMessageResponseData(conversationId: String, id: String, bodyText: String) -> Data {
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

    private func message(id: String, conversationId: String, bodyText: String) -> DirectMessage {
        try! decodeJSON(
            """
            {
              "id": "\(id)",
              "conversation_id": "\(conversationId)",
              "body_text": "\(bodyText)",
              "created_by_id": "user-1",
              "sender_username": "alice",
              "created_at": "2026-01-01T00:00:00Z",
              "updated_at": "2026-01-01T00:00:00Z",
              "deleted_at": null
            }
            """
        )
    }

    private func decodeJSON<T: Decodable>(_ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(T.self, from: Data(json.utf8))
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
}

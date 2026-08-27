import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelStaleLoadTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testLoadThreadIgnoresStaleResultsAfterSwitchingConversations() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2"] = (
            directConversationResponseData(id: "conversation-2", participantAddPolicy: .allMembers),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2/messages"] = (
            messagesPage(conversationId: "conversation-2", ids: ["message-b"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2/participants"] = (
            participantsPage(conversationId: "conversation-2", ownerUserId: "user-2"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1"] = [
            (directConversationResponseData(id: "conversation-1", participantAddPolicy: .ownerOnly), 200, 0.2)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (messagesPage(conversationId: "conversation-1", ids: ["message-a"]), 200, 0.2)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/participants"] = [
            (participantsPage(conversationId: "conversation-1", ownerUserId: "user-1"), 200, 0.2)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let firstLoad = Task {
            await viewModel.loadThread(conversationId: "conversation-1")
        }
        try await waitForSelectedConversation("conversation-1", in: viewModel)
        await viewModel.loadThread(conversationId: "conversation-2")
        _ = await firstLoad.value

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        let capturedPaths = CannedFeedURLProtocol.capturedURLs.map(\.path)
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-b"], capturedPaths.joined(separator: ","))
        XCTAssertEqual(viewModel.participants.first?.userId, "user-2", capturedPaths.joined(separator: ","))
        XCTAssertEqual(viewModel.participantAddPolicy, .allMembers)
        assertLoadState(viewModel.threadState, .loaded)
    }

    func testCreateConversationClearsOldThreadPaginationBeforeLoadingNewThread() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-old"] = (
            directConversationResponseData(id: "conversation-old", participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-old/messages"] = (
            messagesPage(
                conversationId: "conversation-old",
                ids: ["message-old-1"],
                endCursor: "cursor-old",
                hasNextPage: true
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-old/participants"] = (
            participantsPage(conversationId: "conversation-old", ownerUserId: "user-1"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (
                conversationEnvelope(id: "conversation-99"),
                200,
                0
            ),
            (
                Data(
                    """
                    {
                      "results": [
                        {
                          "id": "conversation-99",
                          "channel_type": null,
                          "title": "Support follow-up",
                          "created_at": "2026-01-01T00:00:00Z",
                          "created_by_id": "user-1",
                          "updated_at": "2026-01-02T00:00:00Z",
                          "participant_usernames": ["alice", "bob"],
                          "participant_add_policy": "all_members"
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
                messagesPage(conversationId: "conversation-99", ids: ["message-99"]),
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

        await viewModel.loadThread(conversationId: "conversation-old")
        _ = await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")

        let requests = zip(CannedFeedURLProtocol.capturedMethods, CannedFeedURLProtocol.capturedURLs)
        let reloadURL = requests.first { method, url in
            method == "GET" && url.path == "/api/v1/my/messages/conversation-99/messages"
        }?.1
        let reloadQueryItems = reloadURL.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems
        }
        XCTAssertNotNil(reloadURL)
        XCTAssertNil(reloadQueryItems?.first(where: { $0.name == "after" })?.value)
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-99")
        XCTAssertEqual(viewModel.messages.first?.id, "message-99")
        assertLoadState(viewModel.threadState, .loaded)
    }

    func testStaleParticipantMutationResultsAreIgnoredAfterConversationSwitch() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2"] = (
            directConversationResponseData(id: "conversation-2", participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2/messages"] = (
            messagesPage(conversationId: "conversation-2", ids: ["message-b"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2/participants"] = (
            participantsPage(conversationId: "conversation-2", ownerUserId: "user-3"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/participants"] = [
            (participantAddResponseData(), 200, 0.2)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"

        let addTask = Task {
            await viewModel.addParticipant(userId: "user-2")
        }
        try await waitForCapturedPath(
            "/api/v1/my/messages/conversation-1/participants",
            count: 1
        )
        await viewModel.loadThread(conversationId: "conversation-2")
        _ = await addTask.value

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.participants.map(\.userId), ["user-3"])
        XCTAssertEqual(viewModel.participantAddPolicy, .ownerOnly)
        assertLoadState(viewModel.threadState, .loaded)
    }

    func testStaleParticipantPolicyErrorsAreIgnoredAfterConversationSwitch() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2"] = (
            directConversationResponseData(id: "conversation-2", participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2/messages"] = (
            messagesPage(conversationId: "conversation-2", ids: ["message-b"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2/participants"] = (
            participantsPage(conversationId: "conversation-2", ownerUserId: "user-3"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1"] = [
            (Data("{}".utf8), 500, 0.2)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.participantAddPolicy = .ownerOnly

        let policyTask = Task {
            await viewModel.updatePolicy(.allMembers)
        }
        try await waitForCapturedPath("/api/v1/my/messages/conversation-1", count: 1)
        await viewModel.loadThread(conversationId: "conversation-2")
        _ = await policyTask.value

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.participantAddPolicy, .ownerOnly)
        assertLoadState(viewModel.threadState, .loaded)
    }

    func testConcurrentOlderMessageLoadsDoNotDuplicateRequestsOrPages() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(id: "conversation-1", participantAddPolicy: .ownerOnly),
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
            (
                messagesPage(
                    conversationId: "conversation-1",
                    ids: ["message-2"],
                    endCursor: nil,
                    hasNextPage: false
                ),
                200,
                0.2
            ),
            (
                messagesPage(
                    conversationId: "conversation-1",
                    ids: ["message-3"],
                    endCursor: nil,
                    hasNextPage: false
                ),
                200,
                0
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadThread(conversationId: "conversation-1")
        let firstLoad = Task {
            await viewModel.loadMoreMessages()
        }
        try await Task.sleep(nanoseconds: 50_000_000)
        let secondLoad = Task {
            await viewModel.loadMoreMessages()
        }
        _ = await firstLoad.value
        _ = await secondLoad.value

        let messageRequestCount = CannedFeedURLProtocol.capturedURLs
            .filter { $0.path == "/api/v1/my/messages/conversation-1/messages" }
            .count
        XCTAssertEqual(messageRequestCount, 2)
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-2", "message-1"])
        assertLoadState(viewModel.threadState, .loaded)
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

    private func conversationEnvelope(id: String) -> Data {
        Data(
            """
            {
              "conversation": {
                "id": "\(id)",
                "channel_type": null,
                "title": "New thread",
                "created_at": "2026-01-01T00:00:00Z",
                "created_by_id": "user-1",
                "updated_at": "2026-01-01T00:00:00Z",
                "participant_usernames": ["alice", "bob"],
                "participant_add_policy": "all_members"
              }
            }
            """.utf8
        )
    }

    private func directMessageResponseData(
        conversationId: String,
        id: String,
        bodyText: String
    ) -> Data {
        Data(
            """
            {
              "message": \(messageJSON(conversationId: conversationId, id: id, bodyText: bodyText))
            }
            """.utf8
        )
    }

    private func participantAddResponseData() -> Data {
        Data(
            """
            {
              "participant": {
                "id": "participant-user-2",
                "conversation_id": "conversation-1",
                "user_id": "user-2",
                "role": "member",
                "created_at": "2026-01-01T00:00:00Z",
                "removed_at": null,
                "username": "bob",
                "profile_image_id": null
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
        Data(
            """
            {
              "results": [
                \(ids.map { messageJSON(conversationId: conversationId, id: $0) }.joined(separator: ","))
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

    private func messageJSON(
        conversationId: String,
        id: String,
        bodyText: String = "Hello native"
    ) -> String {
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

    private func waitForSelectedConversation(
        _ conversationId: String,
        in viewModel: DirectMessagesViewModel,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async throws {
        for _ in 0 ..< 20 {
            if viewModel.selectedConversationId == conversationId {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for selected conversation", file: file, line: line)
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

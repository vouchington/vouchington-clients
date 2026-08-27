import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelCreateRaceTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testCreateConversationDoesNotReplaceCurrentThreadWhenSelectionChangesBeforeInitialSendCompletes() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationEnvelope(id: "conversation-99"), 200, 0),
            (emptyConversationPage(), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (directMessageResponseData(conversationId: "conversation-99", id: "message-99"), 200, 0.2)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            participantsPage(conversationId: "conversation-99"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99"] = (
            conversationEnvelope(id: "conversation-99"),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let createTask = Task {
            await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")
        }
        try await waitForCapturedPath("/api/v1/my/messages/conversation-99/messages", count: 1)
        viewModel.selectedConversationId = "conversation-2"
        viewModel.messages = [message(id: "message-old", conversationId: "conversation-2")]

        let result = await createTask.value

        XCTAssertEqual(result, .created)
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-old"])
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-99"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs
                .filter { $0.path == "/api/v1/my/messages/conversation-99/messages" }
                .count,
            1
        )
    }

    func testCreateConversationKeepsFallbackConversationVisibleWhenInboxRefreshFails() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationEnvelope(id: "conversation-99", title: "", usernamesJSON: "null"), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (directMessageResponseData(conversationId: "conversation-99", id: "message-99"), 200, 0),
            (messagesPage(conversationId: "conversation-99", ids: ["message-99"]), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            emptyParticipantsPage(conversationId: "conversation-99"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99"] = (
            conversationEnvelope(
                id: "conversation-99",
                title: "Alice, Bob",
                usernamesJSON: #"["alice","bob"]"#
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let result = await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")

        XCTAssertEqual(result, .created)
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-99")
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-99"])
        XCTAssertEqual(viewModel.conversations.first?.title, "Alice, Bob")
        XCTAssertEqual(viewModel.conversations.first?.participantUsernames, ["alice", "bob"])
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-99"])
        assertLoadState(viewModel.threadState, .loaded)
        assertLoadState(viewModel.state, .error(.api(statusCode: 500, preconditionCode: nil)))
    }

    func testCreateConversationReplacesExistingInboxRowWithHydratedFallbackWhenRefreshFails() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationEnvelope(id: "conversation-99", title: "", usernamesJSON: "null"), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (directMessageResponseData(conversationId: "conversation-99", id: "message-99"), 200, 0),
            (messagesPage(conversationId: "conversation-99", ids: ["message-99"]), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            participantsPage(conversationId: "conversation-99"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99"] = (
            conversationEnvelope(
                id: "conversation-99",
                title: "Hydrated fallback",
                usernamesJSON: #"["alice","bob"]"#
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.conversations = [
            conversation(
                id: "conversation-99",
                title: "Stale inbox row",
                updatedAt: "2026-01-01T00:00:00Z",
                participantUsernames: ["stale"]
            )
        ]

        let result = await viewModel.createConversation(userIds: ["user-2"], text: "Hello native")

        XCTAssertEqual(result, .created)
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-99"])
        XCTAssertEqual(viewModel.conversations.first?.title, "Hydrated fallback")
        XCTAssertEqual(viewModel.conversations.first?.participantUsernames, ["bob"])
    }

    func testCreateConversationLoadsCreatedThreadWhenInitialSendFails() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationEnvelope(id: "conversation-99", title: "", usernamesJSON: "null"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (Data("{}".utf8), 500, 0),
            (messagesPage(conversationId: "conversation-99", ids: []), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            participantsPage(conversationId: "conversation-99"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99"] = (
            conversationEnvelope(id: "conversation-99", title: "", usernamesJSON: "null"),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let result = await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")

        XCTAssertEqual(result, .failed(createdConversationId: "conversation-99"))
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-99")
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-99"])
        XCTAssertEqual(viewModel.conversations.first?.participantUsernames, ["bob"])
        XCTAssertEqual(viewModel.messages.map(\.id), [])
        assertLoadState(viewModel.threadState, .loaded)
        assertLoadState(viewModel.state, .error(.api(statusCode: 0, preconditionCode: nil)))
    }

    func testCreateConversationKeepsLocalSenderLabelWhenThreadReloadFails() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationEnvelope(id: "conversation-99", title: "", usernamesJSON: "null"), 200, 0),
            (emptyConversationPage(), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (
                Data(
                    """
                    {
                      "message": {
                        "id": "message-99",
                        "conversation_id": "conversation-99",
                        "body_text": "Hello native",
                        "created_by_id": "user-1",
                        "created_at": "2026-01-01T00:00:00Z",
                        "updated_at": "2026-01-01T00:00:00Z",
                        "deleted_at": null
                      }
                    }
                    """.utf8
                ),
                200,
                0
            ),
            (Data("{}".utf8), 500, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            participantsPage(conversationId: "conversation-99"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99"] = (
            conversationEnvelope(id: "conversation-99"),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let result = await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")

        XCTAssertEqual(result, .created)
        XCTAssertEqual(viewModel.messages.first?.senderUsername, "You")
        assertLoadState(viewModel.threadState, .error(.api(statusCode: 500, preconditionCode: nil)))
    }

    func testCreateConversationSkipsReloadAfterSelectionChangesDuringFallback() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationEnvelope(id: "conversation-99", title: "", usernamesJSON: "null"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (Data("{}".utf8), 500, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/participants"] = [
            (participantsPage(conversationId: "conversation-99"), 200, 0.2)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99"] = [
            (conversationEnvelope(id: "conversation-99"), 200, 0.2)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-2"
        viewModel.messages = [message(id: "message-old", conversationId: "conversation-2")]

        let createTask = Task {
            await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")
        }
        try await waitForCapturedPath("/api/v1/my/messages/conversation-99/participants", count: 1)
        viewModel.selectedConversationId = "conversation-2"
        viewModel.messages = [message(id: "message-old", conversationId: "conversation-2")]

        let result = await createTask.value

        XCTAssertEqual(result, .failed(createdConversationId: "conversation-99"))
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-old"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter {
                $0.path == "/api/v1/my/messages/conversation-99/messages"
            }.count,
            1
        )
    }

    func testCreateConversationDoesNotClobberRefreshedInboxConversation() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationEnvelope(id: "conversation-99", title: ""), 200, 0),
            (conversationPage(id: "conversation-99", title: "Alice, Bob", updatedAt: "2026-01-03T00:00:00Z"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (directMessageResponseData(conversationId: "conversation-99", id: "message-99"), 200, 0),
            (messagesPage(conversationId: "conversation-99", ids: ["message-99"]), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            participantsPage(conversationId: "conversation-99"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99"] = (
            conversationEnvelope(id: "conversation-99"),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let result = await viewModel.createConversation(userIds: ["user-2"], text: "Hello native")

        XCTAssertEqual(result, .created)
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-99"])
        XCTAssertEqual(viewModel.conversations.first?.title, "Alice, Bob")
        XCTAssertEqual(viewModel.conversations.first?.updatedAt, isoDate("2026-01-03T00:00:00Z"))
    }

    private func conversationEnvelope(id: String) -> Data {
        Data(
            """
            {
              "conversation": \(conversationJSON(id: id))
            }
            """.utf8
        )
    }

    private func conversationEnvelope(id: String, title: String) -> Data {
        conversationEnvelope(id: id, title: title, usernamesJSON: #"["alice","bob"]"#)
    }

    private func conversationEnvelope(id: String, title: String, usernamesJSON: String) -> Data {
        Data(
            """
            {
              "conversation": \(conversationJSON(
                  id: id,
                  title: title,
                  updatedAt: "2026-01-01T00:00:00Z",
                  usernamesJSON: usernamesJSON
              ))
            }
            """.utf8
        )
    }

    private func conversation(
        id: String,
        title: String,
        updatedAt: String,
        participantUsernames: [String]
    ) -> DirectConversation {
        try! decodeJSON(
            """
            {
              "id": "\(id)",
              "channel_type": null,
              "title": "\(title)",
              "created_at": "2026-01-01T00:00:00Z",
              "created_by_id": "user-1",
              "updated_at": "\(updatedAt)",
              "participant_usernames": \(jsonArray(participantUsernames)),
              "participant_add_policy": "all_members"
            }
            """
        )
    }

    private func conversationPage(id: String, title: String, updatedAt: String) -> Data {
        Data(
            """
            {
              "results": [
                \(conversationJSON(id: id, title: title, updatedAt: updatedAt))
              ],
              "page_info": {"has_next_page": false, "end_cursor": null, "start_cursor": null}
            }
            """.utf8
        )
    }

    private func emptyConversationPage() -> Data {
        Data(
            """
            {
              "results": [],
              "page_info": {"has_next_page": false, "end_cursor": null, "start_cursor": null}
            }
            """.utf8
        )
    }

    private func messagesPage(conversationId: String, ids: [String]) -> Data {
        Data(
            """
            {
              "results": [
                \(ids.map { messageJSON(id: $0, conversationId: conversationId) }.joined(separator: ","))
              ],
              "page_info": {"has_next_page": false, "end_cursor": null, "start_cursor": null}
            }
            """.utf8
        )
    }

    private func directMessageResponseData(conversationId: String, id: String) -> Data {
        Data(
            """
            {
              "message": \(messageJSON(id: id, conversationId: conversationId))
            }
            """.utf8
        )
    }

    private func participantsPage(conversationId: String) -> Data {
        Data(
            """
            {
              "results": [
                {
                  "id": "participant-owner",
                  "conversation_id": "\(conversationId)",
                  "user_id": "user-1",
                  "role": "owner",
                  "created_at": "2026-01-01T00:00:00Z",
                  "removed_at": null,
                  "username": "alice",
                  "profile_image_id": null
                },
                {
                  "id": "participant-member",
                  "conversation_id": "\(conversationId)",
                  "user_id": "user-2",
                  "role": "member",
                  "created_at": "2026-01-01T00:00:00Z",
                  "removed_at": null,
                  "username": "bob",
                  "profile_image_id": null
                }
              ],
              "page_info": {"has_next_page": false, "end_cursor": null, "start_cursor": null}
            }
            """.utf8
        )
    }

    private func emptyParticipantsPage(conversationId _: String) -> Data {
        Data(
            """
            {
              "results": [],
              "page_info": {"has_next_page": false, "end_cursor": null, "start_cursor": null}
            }
            """.utf8
        )
    }

    private func conversationJSON(id: String) -> String {
        conversationJSON(id: id, title: "Support follow-up", updatedAt: "2026-01-02T00:00:00Z")
    }

    private func conversationJSON(
        id: String,
        title: String,
        updatedAt: String,
        usernamesJSON: String = #"["alice","bob"]"#
    ) -> String {
        """
        {
          "id": "\(id)",
          "channel_type": null,
          "title": "\(title)",
          "created_at": "2026-01-01T00:00:00Z",
          "created_by_id": "user-1",
          "updated_at": "\(updatedAt)",
          "participant_usernames": \(usernamesJSON),
          "participant_add_policy": "all_members"
        }
        """
    }

    private func messageJSON(
        id: String,
        conversationId: String,
        bodyText: String = "Hello native",
        senderUsername: String? = "alice"
    ) -> String {
        """
        {
          "id": "\(id)",
          "conversation_id": "\(conversationId)",
          "body_text": "\(bodyText)",
          "created_by_id": "user-1",
          \(senderUsername.map { "\"sender_username\": \"\($0)\"," } ?? "")
          "created_at": "2026-01-01T00:00:00Z",
          "updated_at": "2026-01-01T00:00:00Z",
          "deleted_at": null
        }
        """
    }

    private func message(id: String, conversationId: String) -> DirectMessage {
        try! decodeJSON(messageJSON(id: id, conversationId: conversationId, bodyText: "Thread B"))
    }

    private func decodeJSON<T: Decodable>(_ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(T.self, from: Data(json.utf8))
    }

    private func isoDate(_ value: String) -> Date {
        ISO8601DateFormatter().date(from: value)!
    }

    private func jsonArray(_ values: [String]) -> String {
        "[" + values.map { "\"\($0)\"" }.joined(separator: ",") + "]"
    }

    private func assertLoadState(_ state: LoadState, _ expected: LoadState) {
        switch (state, expected) {
        case (.idle, .idle), (.loading, .loading), (.loaded, .loaded):
            return
        case let (.error(lhs), .error(rhs)):
            XCTAssertEqual(lhs.localizedDescription, rhs.localizedDescription)
        default:
            XCTFail("Unexpected state mismatch")
        }
    }

    private func waitForCapturedPath(_ path: String, count: Int) async throws {
        for _ in 0 ..< 40 {
            if CannedFeedURLProtocol.capturedURLs.filter({ $0.path == path }).count >= count {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for captured path \(path)")
    }
}

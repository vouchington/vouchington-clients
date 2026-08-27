import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelParticipantInboxTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testAddParticipantRefreshesInboxConversationRow() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/participants"] = [
            (participantResponse(userId: "user-2", username: "bob"), 200, 0),
            (participantsPage(usernames: ["alice", "bob"]), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            conversationEnvelope(
                title: "",
                updatedAt: "2026-01-03T00:00:00Z",
                usernames: nil
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversations = [
            conversation(title: "Alice", updatedAt: "2026-01-01T00:00:00Z", usernames: ["alice"])
        ]

        await viewModel.addParticipant(userId: "user-2")

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "GET", "GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/messages/conversation-1/participants",
            "/api/v1/my/messages/conversation-1",
            "/api/v1/my/messages/conversation-1/participants"
        ])
        XCTAssertEqual(viewModel.participants.map(\.userId), ["user-2"])
        XCTAssertEqual(viewModel.conversations.first?.participantUsernames, ["bob"])
        XCTAssertEqual(viewModel.conversations.first?.updatedAt, isoDate("2026-01-03T00:00:00Z"))
        XCTAssertEqual(
            try DirectMessagesSurface(viewModel: viewModel, routeMatch: nil)
                .label(for: XCTUnwrap(viewModel.conversations.first)),
            "bob"
        )
    }

    func testRemoveParticipantRefreshesInboxConversationRow() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants/user-2"] = (
            Data("{}".utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPage(usernames: ["alice"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            conversationEnvelope(
                title: "",
                updatedAt: "2026-01-04T00:00:00Z",
                usernames: nil
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.participants = [participant(userId: "user-2", username: "bob")]
        viewModel.conversations = [
            conversation(title: "Alice, Bob", updatedAt: "2026-01-01T00:00:00Z", usernames: ["alice", "bob"])
        ]

        await viewModel.removeParticipant(userId: "user-2")

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["DELETE", "GET", "GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/messages/conversation-1/participants/user-2",
            "/api/v1/my/messages/conversation-1",
            "/api/v1/my/messages/conversation-1/participants"
        ])
        XCTAssertTrue(viewModel.participants.isEmpty)
        XCTAssertNil(viewModel.conversations.first?.participantUsernames)
        XCTAssertEqual(viewModel.conversations.first?.updatedAt, isoDate("2026-01-04T00:00:00Z"))
        XCTAssertEqual(
            try DirectMessagesSurface(viewModel: viewModel, routeMatch: nil)
                .label(for: XCTUnwrap(viewModel.conversations.first)),
            ""
        )
    }

    private func conversationEnvelope(
        title: String,
        updatedAt: String,
        usernames: [String]?
    ) -> Data {
        let usernamesJSON = usernames.map(jsonArray) ?? "null"
        return Data(
            """
            {
              "conversation": \(conversationJSON(title: title, updatedAt: updatedAt, usernames: usernamesJSON))
            }
            """.utf8
        )
    }

    private func participantsPage(usernames: [String]) -> Data {
        Data(
            """
            {
              "results": [
                \(usernames.enumerated().map { index, username in
                    """
                    {
                      "id": "participant-\(index)",
                      "conversation_id": "conversation-1",
                      "user_id": "\(index == 0 ? "user-1" : "user-\(index + 1)")",
                      "role": "\(index == 0 ? "owner" : "member")",
                      "created_at": "2026-01-01T00:00:00Z",
                      "removed_at": null,
                      "username": "\(username)",
                      "profile_image_id": null
                    }
                    """
                }.joined(separator: ","))
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

    private func participantResponse(userId: String, username: String) -> Data {
        Data(
            """
            {
              "participant": {
                "id": "participant-\(userId)",
                "conversation_id": "conversation-1",
                "user_id": "\(userId)",
                "role": "member",
                "created_at": "2026-01-01T00:00:00Z",
                "removed_at": null,
                "username": "\(username)",
                "profile_image_id": null
              }
            }
            """.utf8
        )
    }

    private func participant(userId: String, username: String) -> ConversationParticipant {
        try! decodeJSON(
            """
            {
              "id": "participant-\(userId)",
              "conversation_id": "conversation-1",
              "user_id": "\(userId)",
              "role": "member",
              "created_at": "2026-01-01T00:00:00Z",
              "removed_at": null,
              "username": "\(username)",
              "profile_image_id": null
            }
            """
        )
    }

    private func conversation(title: String, updatedAt: String, usernames: [String]) -> DirectConversation {
        try! decodeJSON(conversationJSON(title: title, updatedAt: updatedAt, usernames: jsonArray(usernames)))
    }

    private func conversationJSON(title: String, updatedAt: String, usernames: String) -> String {
        """
        {
          "id": "conversation-1",
          "channel_type": "direct",
          "title": "\(title)",
          "created_at": "2026-01-01T00:00:00Z",
          "created_by_id": "user-1",
          "updated_at": "\(updatedAt)",
          "participant_usernames": \(usernames),
          "participant_add_policy": "all_members"
        }
        """
    }

    private func jsonArray(_ values: [String]) -> String {
        "[" + values.map { "\"\($0)\"" }.joined(separator: ",") + "]"
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
}

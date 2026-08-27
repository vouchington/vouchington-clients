import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelInboxFallbackTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testSendMessageUpsertsConversationFallbackWhenInboxRefreshFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (
            directMessageResponseData(
                conversationId: "conversation-1",
                id: "message-2",
                bodyText: "Reply from native"
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            conversationEnvelope(
                id: "conversation-1",
                title: "Fresh follow-up",
                participantUsernames: ["bob"]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPage(conversationId: "conversation-1"),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversations = [
            conversation(
                id: "conversation-1",
                title: "Stale follow-up",
                updatedAt: "2026-01-01T00:00:00Z",
                participantUsernames: ["old"]
            )
        ]

        let sent = await viewModel.sendMessage(text: "  Reply from native  ")

        XCTAssertTrue(sent)
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])
        XCTAssertEqual(viewModel.conversations.first?.title, "Fresh follow-up")
        XCTAssertEqual(viewModel.conversations.first?.participantUsernames, ["bob"])
        XCTAssertEqual(viewModel.conversations.first?.updatedAt, isoDate("2026-01-01T00:00:00Z"))
    }

    func testParticipantRefreshReordersConversationByUpdatedAt() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/participants"] = [
            (
                addParticipantResponseData(),
                200,
                0
            ),
            (
                participantsPage(conversationId: "conversation-1"),
                200,
                0
            )
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            conversationEnvelope(
                id: "conversation-1",
                title: "Fresh follow-up",
                updatedAt: "2026-01-03T00:00:00Z",
                participantUsernames: ["bob"]
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversations = [
            conversation(
                id: "conversation-2",
                title: "Older follow-up",
                updatedAt: "2026-01-02T00:00:00Z",
                participantUsernames: ["carol"]
            ),
            conversation(
                id: "conversation-1",
                title: "Stale follow-up",
                updatedAt: "2026-01-01T00:00:00Z",
                participantUsernames: ["old"]
            )
        ]

        await viewModel.addParticipant(userId: "user-2")

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1", "conversation-2"])
        XCTAssertEqual(viewModel.conversations.first?.updatedAt, isoDate("2026-01-03T00:00:00Z"))
    }

    private func conversationEnvelope(id: String, title: String, participantUsernames: [String]) -> Data {
        Data(
            """
            {
              "conversation": \(conversationJSON(
                  id: id,
                  title: title,
                  updatedAt: "2026-01-01T00:00:00Z",
                  participantUsernames: participantUsernames
              ))
            }
            """.utf8
        )
    }

    private func conversationEnvelope(
        id: String,
        title: String,
        updatedAt: String,
        participantUsernames: [String]
    ) -> Data {
        Data(
            """
            {
              "conversation": \(conversationJSON(
                  id: id,
                  title: title,
                  updatedAt: updatedAt,
                  participantUsernames: participantUsernames
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
        try! decodeJSON(conversationJSON(
            id: id,
            title: title,
            updatedAt: updatedAt,
            participantUsernames: participantUsernames
        ))
    }

    private func conversationJSON(
        id: String,
        title: String,
        updatedAt: String,
        participantUsernames: [String]
    ) -> String {
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

    private func addParticipantResponseData() -> Data {
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

import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelSelfRemovalTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testSelfRemovalClearsThreadAndRemovesConversationFromInbox() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants/user-1"] = (
            Data("{}".utf8),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.messages = [message(id: "message-1", conversationId: "conversation-1", bodyText: "Hello")]
        viewModel.participants = [participant(userId: "user-1", role: "owner")]
        viewModel.conversations = [conversation(id: "conversation-1")]

        await viewModel.removeParticipant(userId: "user-1")

        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertTrue(viewModel.participants.isEmpty)
        XCTAssertTrue(viewModel.conversations.isEmpty)
    }

    private func conversation(id: String) -> DirectConversation {
        try! decodeJSON(
            """
            {
              "id": "\(id)",
              "channel_type": "direct",
              "title": "Support follow-up",
              "created_at": "2026-01-01T00:00:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-01-01T00:00:00Z",
              "participant_usernames": ["alice", "bob"],
              "participant_add_policy": "all_members"
            }
            """
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

    private func participant(userId: String, role: String) -> ConversationParticipant {
        try! decodeJSON(
            """
            {
              "id": "participant-owner",
              "conversation_id": "conversation-1",
              "user_id": "\(userId)",
              "role": "\(role)",
              "created_at": "2026-01-01T00:00:00Z",
              "removed_at": null,
              "username": "alice",
              "profile_image_id": null
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
}

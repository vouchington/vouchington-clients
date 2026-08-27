import Foundation
@testable import VouchaModels
import XCTest

final class UserFacingMessageModelDecodingTests: XCTestCase {
    private let decoder = makeVouchaDecoder()

    func testDecodesMessageFixtures() throws {
        let conversation = try decoder.decode(
            DirectConversationResponse.self,
            from: ApiFixtureLoader.data("native.messages.conversation.default")
        )
        XCTAssertEqual(conversation.conversation.id, "00000000-0000-7000-8000-000000000101")
        XCTAssertEqual(conversation.conversation.participantAddPolicy, .allMembers)

        let conversations = try decoder.decode(
            Page<DirectConversation>.self,
            from: ApiFixtureLoader.data("native.messages.conversations.default")
        )
        XCTAssertEqual(conversations.results.count, 2)
        XCTAssertEqual(conversations.results.first?.participantUsernames, ["alice", "bob"])

        let thread = try decoder.decode(
            Page<DirectMessage>.self,
            from: ApiFixtureLoader.data("native.messages.thread.default")
        )
        XCTAssertEqual(thread.results.first?.bodyText, "Hello from Alice")
        XCTAssertEqual(thread.results.last?.senderUsername, "fixtureuser")

        let participants = try decoder.decode(
            Page<ConversationParticipant>.self,
            from: ApiFixtureLoader.data("native.messages.participants.default")
        )
        XCTAssertEqual(participants.results.first?.username, "fixtureuser")
        XCTAssertEqual(participants.results.last?.profileImageId, "image-alice")

        let send = try decoder.decode(
            DirectMessageResponse.self,
            from: ApiFixtureLoader.data("native.messages.send.default")
        )
        XCTAssertEqual(send.message.bodyText, "Sent from native")

        let add = try decoder.decode(
            ConversationParticipantResponse.self,
            from: ApiFixtureLoader.data("native.messages.participant-add.default")
        )
        XCTAssertEqual(add.participant.username, "bob")

        let policy = try decoder.decode(
            ConversationParticipantPolicyResponse.self,
            from: ApiFixtureLoader.data("native.messages.policy.default")
        )
        XCTAssertEqual(policy.participantAddPolicy, .ownerOnly)

        let search = try decoder.decode(
            Page<PublicUser>.self,
            from: ApiFixtureLoader.data("native.messages.user-search.default")
        )
        XCTAssertEqual(search.results.first?.username, "bob")
        XCTAssertEqual(search.results.last?.profileImageId, "image-bonnie")
    }

    func testDirectMessageInitUsesCreatedAtWhenUpdatedAtIsMissing() throws {
        let createdAt = try decoder.decode(Date.self, from: Data(#""2026-01-01T12:00:00Z""#.utf8))

        let message = DirectMessage(
            id: "message-1",
            conversationId: "conversation-1",
            bodyText: "Native body",
            createdById: "user-1",
            senderUsername: "alice",
            createdAt: createdAt,
            updatedAt: nil,
            deletedAt: nil
        )

        XCTAssertEqual(message.updatedAt, createdAt)
        XCTAssertEqual(message.senderUsername, "alice")
    }

    func testOptimisticDirectMessageBuildsDraftIdentifier() {
        let optimistic = DirectMessage.optimistic(
            conversationId: "conversation-1",
            bodyText: "Draft body",
            createdById: "user-1"
        )

        XCTAssertTrue(optimistic.id.hasPrefix("optimistic-"))
        XCTAssertEqual(optimistic.conversationId, "conversation-1")
        XCTAssertEqual(optimistic.bodyText, "Draft body")
        XCTAssertEqual(optimistic.createdById, "user-1")
        XCTAssertNil(optimistic.senderUsername)
        XCTAssertNil(optimistic.deletedAt)
    }
}

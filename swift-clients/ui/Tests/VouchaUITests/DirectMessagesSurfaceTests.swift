import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class DirectMessagesSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testInboxRouteRendersConversationListBranch() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages"))
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.hasMoreConversations = true
        let sut = DirectMessagesSurface(viewModel: viewModel, routeMatch: route.match)

        XCTAssertNoThrow(try sut.inspect().find(text: "No messages"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Load more"))
    }

    func testInboxConversationRowLoadsThreadWhenTapped() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages"))
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(participantAddPolicy: .allMembers),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (
            messagesPageData,
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPageData,
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.conversations = [conversation(id: "conversation-1", participantAddPolicy: .allMembers)]
        viewModel.hasMoreConversations = false
        let sut = DirectMessagesSurface(viewModel: viewModel, routeMatch: route.match)

        await sut.openConversation("conversation-1")

        for _ in 0 ..< 20 {
            if CannedFeedURLProtocol.capturedURLs.contains(where: { $0.path == "/api/v1/my/messages/conversation-1" }),
               CannedFeedURLProtocol.capturedURLs.contains(where: {
                   $0.path == "/api/v1/my/messages/conversation-1/messages"
               }),
               CannedFeedURLProtocol.capturedURLs.contains(where: {
                   $0.path == "/api/v1/my/messages/conversation-1/participants"
               }) {
                break
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/my/messages/conversation-1" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/my/messages/conversation-1/messages" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/my/messages/conversation-1/participants" })
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-1")
    }

    func testThreadInboxButtonReturnsToInbox() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (
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
                200,
                0
            )
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.messages = [message(id: "message-1", conversationId: "conversation-1", bodyText: "Hello")]
        viewModel.participants = [participant(id: "participant-owner", userId: "user-1", role: "owner")]
        let sut = DirectMessagesSurface(
            viewModel: viewModel,
            routeMatch: route.match,
            initialSelectedConversationId: "conversation-1"
        )

        let buttons = try sut.inspect().findAll(ViewType.Button.self)
        XCTAssertFalse(buttons.isEmpty)
        try buttons[0].tap()

        for _ in 0 ..< 20 {
            if viewModel.selectedConversationId == nil, viewModel.messages.isEmpty {
                break
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }

        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertTrue(viewModel.messages.isEmpty)
    }

    func testNewMessageRouteRendersRecipientComposerBranch() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/new"))
        let sut = try DirectMessagesSurface(client: makeClient(), routeMatch: route.match, currentUserId: "user-1")

        XCTAssertNoThrow(try sut.inspect().find(text: "Recipients"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Message"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Send"))
    }

    func testComposeSendClearsDraftAfterSuccessfulConversationCreation() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/new"))
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (
                Data(
                    #"{"conversation":{"id":"conversation-99","channel_type":"direct","title":"New thread","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:00Z","participant_usernames":["alice","bob"],"participant_add_policy":"all_members"}}"#
                        .utf8
                ),
                200,
                0
            ),
            (
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
            (messagesPageData(conversationId: "conversation-99"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            participantsPageData,
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99"] = (
            directConversationResponseData(
                conversationId: "conversation-99",
                participantAddPolicy: .allMembers
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        let sut = DirectMessagesSurface(
            viewModel: viewModel,
            routeMatch: route.match,
            initialSelectedRecipients: [publicUser(id: "user-2", username: "bob")],
            initialMessageText: "Hello native"
        )

        try sut.inspect().find(button: "Send").tap()

        for _ in 0 ..< 20 {
            if sut.composeState.selectedRecipients.isEmpty, sut.composeState.messageText(for: nil).isEmpty {
                break
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }

        XCTAssertTrue(sut.composeState.selectedRecipients.isEmpty)
        XCTAssertTrue(sut.composeState.messageText(for: nil).isEmpty)
        XCTAssertNoThrow(try sut.inspect().find(text: "Participants"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Inbox"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Recipients"))
    }

    func testThreadSendFailurePreservesDraftText() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (
            Data("{}".utf8),
            500
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        let sut = DirectMessagesSurface(
            viewModel: viewModel,
            routeMatch: route.match,
            initialMessageText: "Failed draft",
            initialSelectedConversationId: "conversation-1"
        )

        try sut.inspect().find(button: "Send").tap()
        try await Task.sleep(nanoseconds: 200_000_000)

        XCTAssertEqual(sut.composeState.messageText(for: "conversation-1"), "Failed draft")
    }

    func testThreadSendCompletionPreservesDraftTypedWhileRequestIsInFlight() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                directMessageResponseData(
                    conversationId: "conversation-1",
                    id: "message-2",
                    bodyText: "Sent draft"
                ),
                200,
                0.2
            )
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        let sut = DirectMessagesSurface(
            viewModel: viewModel,
            routeMatch: route.match,
            initialMessageText: "Sent draft",
            initialSelectedConversationId: "conversation-1"
        )

        try sut.inspect().find(button: "Send").tap()
        for _ in 0 ..< 40 {
            if CannedFeedURLProtocol.capturedURLs
                .filter({ $0.path == "/api/v1/my/messages/conversation-1/messages" }).count >= 1 {
                break
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        sut.composeState.setMessageText("Next draft", for: "conversation-1")

        for _ in 0 ..< 40 {
            if viewModel.messages.contains(where: { $0.id == "message-2" }) {
                break
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }

        XCTAssertEqual(sut.composeState.messageText(for: "conversation-1"), "Next draft")
    }

    func testOpeningDifferentThreadScopesDraftAndClearsParticipantSearchResults() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2"] = (
            directConversationResponseData(conversationId: "conversation-2", participantAddPolicy: .ownerOnly),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2/messages"] = (
            messagesPageData(conversationId: "conversation-2"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-2/participants"] = (
            participantsPageData,
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.participantUserResults = [publicUser(id: "user-2", username: "bob")]
        let sut = DirectMessagesSurface(
            viewModel: viewModel,
            routeMatch: route.match,
            initialMessageText: "Thread one draft",
            initialParticipantQuery: "bo",
            initialSelectedConversationId: "conversation-1"
        )

        await sut.openConversation("conversation-2")

        XCTAssertEqual(sut.composeState.messageText(for: "conversation-1"), "Thread one draft")
        XCTAssertTrue(sut.composeState.messageText(for: "conversation-2").isEmpty)
        XCTAssertTrue(viewModel.participantUserResults.isEmpty)
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
    }

    func testConversationRouteRendersThreadBranch() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (
                Data(
                    """
                    {
                      "results": [],
                      "page_info": {
                        "has_next_page": true,
                        "end_cursor": null,
                        "start_cursor": null
                      }
                    }
                    """.utf8
                ),
                200,
                0.2
            )
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/participants"] = [
            (
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
                200,
                0.2
            )
        ]
        let sut = try DirectMessagesSurface(client: makeClient(), routeMatch: route.match, currentUserId: "user-1")

        XCTAssertNoThrow(try sut.inspect().find(text: "Participants"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Load older messages"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Send"))
    }

    private var messagesPageData: Data {
        messagesPageData(conversationId: "conversation-1")
    }

    private func messagesPageData(conversationId: String) -> Data {
        Data(
            #"{"results":[{"id":"message-1","conversation_id":"\#(conversationId)","body_text":"Hello native","created_by_id":"user-1","sender_username":"alice","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","deleted_at":null}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8
        )
    }

    private func directMessageResponseData(conversationId: String, id: String, bodyText: String) -> Data {
        Data(
            #"{"message":{"id":"\#(id)","conversation_id":"\#(conversationId)","body_text":"\#(bodyText)","created_by_id":"user-1","sender_username":"alice","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","deleted_at":null}}"#
                .utf8
        )
    }

    private var participantsPageData: Data {
        Data(
            #"{"results":[{"id":"participant-owner","conversation_id":"conversation-1","user_id":"user-1","role":"owner","created_at":"2026-01-01T00:00:00Z","removed_at":null,"username":"alice","profile_image_id":null}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8
        )
    }

    private func directConversationResponseData(
        conversationId: String = "conversation-1",
        participantAddPolicy: ConversationParticipantAddPolicy?
    ) -> Data {
        Data(
            """
            {
              "conversation": {
                "id": "\(conversationId)",
                "channel_type": "direct",
                "title": "Support follow-up",
                "created_at": "2026-01-01T00:00:00Z",
                "created_by_id": "user-1",
                "updated_at": "2026-01-01T00:00:00Z",
                "participant_usernames": ["alice", "bob"],
                "participant_add_policy": \(jsonString(participantAddPolicy?.rawValue))
              }
            }
            """.utf8
        )
    }

    private func conversation(
        id: String,
        participantAddPolicy: ConversationParticipantAddPolicy
    ) -> DirectConversation {
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
              "participant_add_policy": "\(participantAddPolicy.rawValue)"
            }
            """
        )
    }

    private func publicUser(id: String, username: String) -> PublicUser {
        try! decodeJSON(
            """
            {
              "id": "\(id)",
              "username": "\(username)",
              "display_name_source": null,
              "use_display_name_from": null,
              "roles": [],
              "profile_image_id": null,
              "markdown": null
            }
            """
        )
    }

    private func participant(id: String, userId: String?, role: String) -> ConversationParticipant {
        try! decodeJSON(
            """
            {
              "id": "\(id)",
              "conversation_id": "conversation-1",
              "user_id": \(jsonString(userId)),
              "role": "\(role)",
              "created_at": "2026-01-01T00:00:00Z",
              "removed_at": null,
              "username": "alice",
              "profile_image_id": null
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
}

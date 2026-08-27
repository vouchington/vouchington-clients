import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesSurfaceRegressionTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testFailedInitialSendRetainsCreatedConversationForReplyRetry() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/new"))
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (
                Data(
                    """
                    {
                      "conversation": {
                        "id": "conversation-99",
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
                          "id": "conversation-99",
                          "channel_type": null,
                          "title": "New thread",
                          "created_at": "2026-01-01T00:00:00Z",
                          "created_by_id": "user-1",
                          "updated_at": "2026-01-01T00:00:00Z",
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
            (Data("{}".utf8), 500, 0),
            (emptyMessagesPageData, 200, 0),
            (
                directMessageResponseData(
                    conversationId: "conversation-99",
                    id: "message-99",
                    bodyText: "Hello native"
                ),
                200,
                0
            )
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
        try await waitForCondition {
            sut.composeState.messageText(for: "conversation-99") == "Hello native"
        }

        XCTAssertEqual(sut.viewModel.selectedConversationId, "conversation-99")
        XCTAssertEqual(sut.composeState.messageText(for: "conversation-99"), "Hello native")
        XCTAssertNoThrow(try sut.inspect().find(button: "Inbox"))

        try sut.inspect().find(button: "Send").tap()
        try await waitForCondition {
            sut.viewModel.messages.last?.id == "message-99" &&
                sut.composeState.messageText(for: "conversation-99").isEmpty
        }

        XCTAssertTrue(sut.composeState.messageText(for: "conversation-99").isEmpty)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count,
            3
        )
        XCTAssertEqual(
            zip(CannedFeedURLProtocol.capturedMethods, CannedFeedURLProtocol.capturedURLs)
                .filter { method, url in method == "POST" && url.path == "/api/v1/my/messages" }
                .count,
            1
        )
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/messages/conversation-99/messages" }
                .count,
            3
        )
        XCTAssertEqual(sut.viewModel.selectedConversationId, "conversation-99")
    }

    func testClearingUnchangedThreadDraftRemovesStoredDraftEntry() {
        let composeState = DirectMessagesComposeState()
        composeState.setMessageText("Thread draft", for: "conversation-1")
        composeState.clearMessageTextIfUnchanged("Thread draft", for: "conversation-1")

        XCTAssertNil(composeState.conversationMessageDrafts["conversation-1"])
        XCTAssertTrue(composeState.messageText(for: "conversation-1").isEmpty)
    }

    func testComposeSendFailureLoadsCreatedThreadAndFallbackInboxRow() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/new"))
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (
                Data(
                    #"{"conversation":{"id":"conversation-99","channel_type":"direct","title":"","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:00Z","participant_usernames":null,"participant_add_policy":"all_members"}}"#
                        .utf8
                ),
                200,
                0
            )
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (Data("{}".utf8), 500, 0),
            (emptyMessagesPageData, 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-99/participants"] = (
            emptyParticipantsPageData,
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

        try await waitForCondition {
            CannedFeedURLProtocol.capturedURLs.contains(where: { $0.path == "/api/v1/my/messages/conversation-99" }) &&
                CannedFeedURLProtocol.capturedURLs.contains(where: {
                    $0.path == "/api/v1/my/messages/conversation-99/messages"
                }) &&
                CannedFeedURLProtocol.capturedURLs.contains(where: {
                    $0.path == "/api/v1/my/messages/conversation-99/participants"
                })
        }

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-99")
        XCTAssertEqual(viewModel.conversations.first?.id, "conversation-99")
        XCTAssertEqual(viewModel.conversations.first?.title, "Support follow-up")
        XCTAssertEqual(viewModel.conversations.first?.participantUsernames, ["alice", "bob"])
        XCTAssertTrue(viewModel.messages.isEmpty)
        switch viewModel.threadState {
        case .loaded:
            break
        default:
            XCTFail("Expected loaded thread state")
        }
        XCTAssertNoThrow(try sut.inspect().find(button: "Inbox"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Recipients"))
    }

    func testNewMessageComposerDoesNotFallBackToInitialConversationAfterInboxTap() {
        XCTAssertNil(
            DirectMessagesSurface.resolveActiveConversationId(
                isShowingInbox: false,
                isShowingNewMessage: true,
                selectedConversationId: nil,
                viewModelSelectedConversationId: nil,
                initialConversationId: "conversation-1"
            )
        )
        XCTAssertEqual(
            DirectMessagesSurface.resolveActiveConversationId(
                isShowingInbox: false,
                isShowingNewMessage: true,
                selectedConversationId: "conversation-99",
                viewModelSelectedConversationId: nil,
                initialConversationId: "conversation-1"
            ),
            "conversation-99"
        )
    }

    func testComposeSendButtonDisablesWhileConversationCreationIsInFlight() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/new"))
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (
                Data(
                    """
                    {
                      "conversation": {
                        "id": "conversation-99",
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
                ),
                200,
                0.2
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
        try await waitForCondition {
            (try? sut.inspect().find(button: "Send").isDisabled()) == true
        }

        try await waitForCondition {
            if sut.viewModel.isCreatingConversation {
                return false
            }
            guard case .loaded = sut.viewModel.threadState else { return false }
            return sut.viewModel.messages.last?.id == "message-99" &&
                sut.composeState.messageText(for: nil).isEmpty
        }

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count, 2)
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

    private var participantsPageData: Data {
        Data(
            """
            {
              "results": [
                {
                  "id": "participant-owner",
                  "conversation_id": "conversation-99",
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

    private var emptyMessagesPageData: Data {
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
        )
    }

    private var emptyParticipantsPageData: Data {
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

    private func jsonString(_ value: String?) -> String {
        guard let value else { return "null" }
        return "\"\(value)\""
    }

    private func decodeJSON<T: Decodable>(_ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(T.self, from: Data(json.utf8))
    }

    private func waitForCondition(
        _ condition: @escaping () -> Bool,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async throws {
        for _ in 0 ..< 40 {
            if condition() {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for condition", file: file, line: line)
    }
}

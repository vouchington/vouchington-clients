import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testLoadInboxReloadAndPaginationUseExpectedConversationEndpoints() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationsPage(ids: ["conversation-1"], endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (conversationsPage(ids: ["conversation-1"], endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (conversationsPage(ids: ["conversation-2"], endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadInbox()
        await viewModel.loadInbox()
        await viewModel.reloadInbox()
        await viewModel.loadMoreConversations()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/messages",
            "/api/v1/my/messages",
            "/api/v1/my/messages"
        ])
        let lastQueryItems = CannedFeedURLProtocol.capturedURLs.last.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems
        }
        XCTAssertEqual(lastQueryItems?.first(where: { $0.name == "after" })?.value, "cursor-1")
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1", "conversation-2"])
        XCTAssertTrue(viewModel.hasMoreConversations == false)
        assertLoadState(viewModel.state, .loaded)
    }

    func testLoadThreadLoadsMessagesAndParticipantsThenOlderMessagesPrepend() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(participantAddPolicy: .ownerOnly),
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
                0
            )
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPage(conversationId: "conversation-1", ownerUserId: "user-1"),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadThread(conversationId: "conversation-1")
        await viewModel.loadMoreMessages()

        let threadPaths = CannedFeedURLProtocol.capturedURLs.map(\.path)
        XCTAssertEqual(threadPaths.filter { $0 == "/api/v1/my/messages/conversation-1" }.count, 1)
        XCTAssertEqual(threadPaths.filter { $0 == "/api/v1/my/messages/conversation-1/messages" }.count, 2)
        XCTAssertEqual(threadPaths.filter { $0 == "/api/v1/my/messages/conversation-1/participants" }.count, 1)
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-1")
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-2", "message-1"])
        XCTAssertEqual(viewModel.participants.first?.role, "owner")
        XCTAssertTrue(viewModel.isOwner)
        XCTAssertTrue(viewModel.canAddParticipants)
        assertLoadState(viewModel.threadState, .loaded)
    }

    func testLoadThreadHydratesParticipantAddPolicyForAllMembersConversation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            directConversationResponseData(participantAddPolicy: .allMembers),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (
            messagesPage(
                conversationId: "conversation-1",
                ids: ["message-1"],
                endCursor: nil,
                hasNextPage: false
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            participantsPage(conversationId: "conversation-1", ownerUserId: "user-2"),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadThread(conversationId: "conversation-1")

        XCTAssertEqual(viewModel.participantAddPolicy, .allMembers)
        XCTAssertTrue(viewModel.canAddParticipants)
    }

    func testCreateConversationAndSendMessageUpdateLoadedThread() async throws {
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
                0
            ),
            (
                conversationsPage(ids: ["conversation-99"], endCursor: nil, hasNextPage: false),
                200,
                0
            ),
            (
                conversationsPage(ids: ["conversation-99"], endCursor: nil, hasNextPage: false),
                200,
                0
            )
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-99/messages"] = [
            (
                directMessageResponseData(
                    conversationId: "conversation-99",
                    id: "message-99",
                    bodyText: "Sent from native"
                ),
                200,
                0
            ),
            (
                messagesPage(
                    conversationId: "conversation-99",
                    ids: ["message-99"],
                    endCursor: nil,
                    hasNextPage: false
                ),
                200,
                0
            ),
            (
                directMessageResponseData(
                    conversationId: "conversation-99",
                    id: "message-100",
                    bodyText: "Reply from native"
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
            directConversationResponseData(
                id: "conversation-99",
                participantAddPolicy: .allMembers
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        _ = await viewModel.createConversation(userIds: ["user-2"], text: "  Hello native  ")
        _ = await viewModel.sendMessage(text: "  Reply from native  ")

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count, 3)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "GET" }.count, 5)
        let paths = CannedFeedURLProtocol.capturedURLs.map(\.path)
        XCTAssertEqual(paths.filter { $0 == "/api/v1/my/messages" }.count, 3)
        XCTAssertEqual(paths.filter { $0 == "/api/v1/my/messages/conversation-99/messages" }.count, 3)
        XCTAssertEqual(paths.filter { $0 == "/api/v1/my/messages/conversation-99/participants" }.count, 1)
        XCTAssertEqual(viewModel.conversations.first?.id, "conversation-99")
        XCTAssertEqual(viewModel.conversations.first?.title, "Support follow-up")
        XCTAssertEqual(viewModel.conversations.first?.participantUsernames, ["alice", "bob"])
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-99")
        XCTAssertEqual(viewModel.messages.last?.bodyText, "Reply from native")
        assertLoadState(viewModel.state, .loaded)
        assertLoadState(viewModel.threadState, .loaded)
    }

    func testSendMessageFailureRemovesOptimisticDraftAndSetsThreadError() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (Data("{}".utf8), 500)
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"

        _ = await viewModel.sendMessage(text: "  Failed reply  ")

        XCTAssertTrue(viewModel.messages.isEmpty)
        assertLoadState(viewModel.threadState, .error(.api(statusCode: 500, preconditionCode: nil)))
    }

    func testParticipantMutationsAppendRemoveAndUpdatePolicy() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            addParticipantResponseData(),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants/user-2"] = (
            Data("{}".utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (
            policyResponseData(),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"
        viewModel.participants = [participant(id: "participant-owner", userId: "user-1", role: "owner")]
        viewModel.participantAddPolicy = .ownerOnly

        await viewModel.addParticipant(userId: "user-2")
        await viewModel.removeParticipant(userId: "user-2")
        await viewModel.updatePolicy(.allMembers)

        XCTAssertEqual(viewModel.participants.map(\.userId).compactMap { $0 }, ["user-1"])
        XCTAssertEqual(viewModel.participantAddPolicy, .allMembers)
        XCTAssertTrue(viewModel.isOwner)
        XCTAssertTrue(viewModel.canAddParticipants)
    }

    func testSearchUsersTrimsQueriesAndClearsBlankInput() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users"] = (userSearchPage(), 200)
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        await viewModel.searchUsers(query: "  bob  ")
        await viewModel.searchUsers(query: "   ")

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/users")
        let queryItems = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems
        }
        XCTAssertEqual(queryItems?.first(where: { $0.name == "q" })?.value, "bob")
        XCTAssertTrue(viewModel.userResults.isEmpty)
    }

    func testNoOpGuardsDoNotSendRequests() async throws {
        let viewModel = try DirectMessagesViewModel(client: makeClient())
        viewModel.hasMoreConversations = false
        viewModel.hasMoreMessages = false

        await viewModel.loadMoreConversations()
        await viewModel.loadMoreMessages()
        _ = await viewModel.createConversation(userIds: [], text: "Hello")
        _ = await viewModel.createConversation(userIds: ["user-2"], text: "   ")
        _ = await viewModel.sendMessage(text: "   ")
        await viewModel.addParticipant(userId: "user-2")
        await viewModel.removeParticipant(userId: "")
        await viewModel.updatePolicy(.allMembers)

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testLoadAndSearchFailuresSetErrorStates() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/users"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            Data("{}".utf8),
            500
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        await viewModel.loadMoreConversations()
        await viewModel.searchUsers(query: "alice")
        await viewModel.loadThread(conversationId: "conversation-1")

        XCTAssertTrue(viewModel.userResults.isEmpty)
        XCTAssertTrue(isErrorState(viewModel.state))
        XCTAssertTrue(isErrorState(viewModel.threadState))
    }

    func testParticipantMutationFailuresSetThreadError() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (
            Data("{}".utf8),
            500
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants/user-2"] = (
            Data("{}".utf8),
            500
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1"] = (Data("{}".utf8), 500)
        let viewModel = try DirectMessagesViewModel(client: makeClient())
        viewModel.selectedConversationId = "conversation-1"

        await viewModel.addParticipant(userId: "user-2")
        await viewModel.removeParticipant(userId: "user-2")
        await viewModel.updatePolicy(.allMembers)

        XCTAssertTrue(viewModel.participants.isEmpty)
        assertLoadState(viewModel.threadState, .error(.api(statusCode: 500, preconditionCode: nil)))
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

    private func conversationsPage(ids: [String], endCursor: String?, hasNextPage: Bool) -> Data {
        Data(
            """
            {
              "results": [
                \(ids.map { conversationJSON(id: $0) }.joined(separator: ","))
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

    private func directConversationResponseData(
        id: String = "conversation-1",
        participantAddPolicy: ConversationParticipantAddPolicy?
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
                "participant_add_policy": \(jsonString(participantAddPolicy?.rawValue))
              }
            }
            """.utf8
        )
    }

    private func messagesPage(
        conversationId: String,
        ids: [String],
        endCursor: String?,
        hasNextPage: Bool
    ) -> Data {
        Data(
            """
            {
              "results": [
                \(ids.map {
                    directMessageJSON(
                        conversationId: conversationId,
                        id: $0,
                        bodyText: $0 == "message-2" ? "Older native message" : "Hello native"
                    )
                }
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

    private func userSearchPage() -> Data {
        Data(
            """
            {
              "results": [
                {
                  "id": "user-2",
                  "username": "bob",
                  "roles": [],
                  "profile_image_id": null,
                  "markdown": null
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

    private func participant(id: String, userId: String?, role: String) -> ConversationParticipant {
        ConversationParticipant(
            id: id,
            conversationId: "conversation-1",
            userId: userId,
            role: role,
            createdAt: Date(timeIntervalSince1970: 1_704_067_200),
            removedAt: nil,
            username: userId == "user-1" ? "alice" : "bob",
            profileImageId: nil
        )
    }

    private func isoDate(_ value: String) -> Date {
        ISO8601DateFormatter().date(from: value)!
    }

    private func decodeJSON<T: Decodable>(_ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(T.self, from: Data(json.utf8))
    }

    private func isErrorState(_ state: LoadState) -> Bool {
        if case .error = state {
            return true
        }
        return false
    }

}

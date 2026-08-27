import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelRegressionTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testReloadInboxPreservesExistingRowsWhenReplacementFetchFails() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages"] = [
            (conversationPageData(id: "conversation-1"), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        await viewModel.loadInbox()
        await viewModel.reloadInbox()

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1"])
        XCTAssertTrue(isErrorState(viewModel.state))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/messages" }.count, 2)
    }

    func testSendMessageKeepsLocalSenderLabelWhenResponseOmitsIt() async throws {
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
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (
            Data(
                """
                {
                  "message": {
                    "id": "message-1",
                    "conversation_id": "conversation-1",
                    "body_text": "Hello native",
                    "created_by_id": "user-1",
                    "created_at": "2026-01-01T00:00:00Z",
                    "updated_at": "2026-01-01T00:00:00Z",
                    "deleted_at": null
                  }
                }
                """.utf8
            ),
            200
        )
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")
        viewModel.selectedConversationId = "conversation-1"

        let sent = await viewModel.sendMessage(text: "Hello native")

        XCTAssertTrue(sent)
        XCTAssertEqual(viewModel.messages.last?.senderUsername, "You")
    }

    private func conversationPageData(id: String) -> Data {
        Data(
            """
            {
              "results": [
                {
                  "id": "\(id)",
                  "channel_type": null,
                  "title": "Support follow-up",
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
        )
    }

    private func isErrorState(_ state: LoadState) -> Bool {
        if case .error = state {
            return true
        }
        return false
    }
}

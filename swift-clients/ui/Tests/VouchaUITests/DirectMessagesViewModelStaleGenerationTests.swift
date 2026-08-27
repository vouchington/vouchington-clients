import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class DirectMessagesViewModelStaleGenerationTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testReloadingSameConversationIgnoresStaleThreadPagesFromEarlierGeneration() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1"] = [
            (directConversationResponseData(participantAddPolicy: .ownerOnly), 200, 0.2),
            (directConversationResponseData(participantAddPolicy: .allMembers), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/messages"] = [
            (messagesPage(ids: ["message-old"]), 200, 0.2),
            (messagesPage(ids: ["message-new"]), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/messages/conversation-1/participants"] = [
            (participantsPage(userId: "user-1"), 200, 0.2),
            (participantsPage(userId: "user-2"), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient(), currentUserId: "user-1")

        let firstLoad = Task { await viewModel.loadThread(conversationId: "conversation-1") }
        try await waitForCapturedPath("/api/v1/my/messages/conversation-1/messages", count: 1)
        let secondLoad = Task { await viewModel.loadThread(conversationId: "conversation-1") }
        _ = await firstLoad.value
        _ = await secondLoad.value

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-1")
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-new"])
        XCTAssertEqual(viewModel.participants.first?.userId, "user-2")
        XCTAssertEqual(viewModel.participantAddPolicy, .allMembers)
    }

    private func directConversationResponseData(participantAddPolicy: ConversationParticipantAddPolicy) -> Data {
        Data(
            """
            {"conversation":{"id":"conversation-1","channel_type":"direct","title":"Support follow-up",
            "created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1",
            "updated_at":"2026-01-01T00:00:00Z","participant_usernames":["alice","bob"],
            "participant_add_policy":"\(participantAddPolicy.rawValue)"}}
            """.utf8
        )
    }

    private func messagesPage(ids: [String]) -> Data {
        Data(
            """
            {"results":[\(ids.map { messageJSON(id: $0) }.joined(separator: ","))],
            "page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
            """.utf8
        )
    }

    private func participantsPage(userId: String) -> Data {
        Data(
            """
            {"results":[{"id":"participant-owner","conversation_id":"conversation-1","user_id":"\(userId)",
            "role":"owner","created_at":"2026-01-01T00:00:00Z","removed_at":null,
            "username":"alice","profile_image_id":null}],
            "page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
            """.utf8
        )
    }

    private func messageJSON(id: String) -> String {
        """
        {"id":"\(id)","conversation_id":"conversation-1","body_text":"Hello native",
        "created_by_id":"user-2","sender_username":"bob","created_at":"2026-01-01T00:00:00Z",
        "updated_at":"2026-01-01T00:00:00Z","deleted_at":null}
        """
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

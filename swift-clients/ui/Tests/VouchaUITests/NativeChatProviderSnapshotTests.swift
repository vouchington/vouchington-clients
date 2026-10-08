@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatProviderSnapshotTests: NativeRouteSurfaceViewModelTestCase {
    func testSendDraftMessageKeepsProviderSelectedBeforeConversationCreationAwait() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/conversations"] = [
            (NativeChatTestFixtures.createdConversationData, 201, 0.1)
        ]
        CannedFeedURLProtocol.contentTypes["/api/v1/conversations/conversation-3/chat"] = "text/event-stream"
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-3/chat"] = (
            Data("event: done\ndata: {}\n\n".utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-3/title"] = (
            NativeChatTestFixtures.renamedConversationData,
            200
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.titleProviderSelection = .anthropic
        viewModel.draftMessage = "Use the selected provider"

        let sendTask = Task { @MainActor in
            await viewModel.sendDraftMessage()
        }
        for _ in 0 ..< 200
            where CannedFeedURLProtocol.capturedURLs.contains(where: {
                $0.path == "/api/v1/conversations"
            }) == false {
            try await Task.sleep(nanoseconds: 1_000_000)
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/conversations" })
        viewModel.titleProviderSelection = .openAI
        await sendTask.value

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedBodies.compactMap { $0 }.contains { $0.contains(#""provider":"anthropic""#) },
            true
        )
    }
}

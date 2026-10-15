import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatProviderStatusReviewTests: NativeRouteSurfaceViewModelTestCase {
    func testUnavailableProviderDetailSurvivesConnectedMCP() async throws {
        let client = try makeClient()
        let session = NativeChatMCPAgentSession(service: PendingTurnMCPService())
        await session.useAccount("member-a")
        try await session.connect()
        let provider = CapturingLocalPersistenceDraftProvider(
            status: .init(isAvailable: false, detail: .verbatim("Choose a model")), response: nil
        )
        let viewModel = NativeChatViewModel(
            client: client, routeMatch: nil, mcpSession: session, currentMemberId: "member-a"
        )
        viewModel.messages = [.init(id: "local-user", role: .user, content: "Hi", isStreaming: false)]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")

        await viewModel.generateLocalDraftMessage(
            client: client, provider: provider,
            context: .init(
                conversationId: "conversation-1", text: "Hi", userMessageId: "local-user",
                createdConversation: false, providerSelection: .openAICompatible(endpointID: UUID())
            )
        )

        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("Choose a model"))
        XCTAssertEqual(provider.generateAssistantResponseCallCount, 1)
    }
}

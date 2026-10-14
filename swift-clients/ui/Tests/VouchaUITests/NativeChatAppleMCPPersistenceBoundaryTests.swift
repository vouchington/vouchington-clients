import Foundation
import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatAppleMCPPersistenceBoundaryTests: NativeRouteSurfaceViewModelTestCase {
    func testToolFreeAppleFallbackRetriesPersistenceAfterMCPRefresh() async throws {
        let path = "/api/v1/conversations/conversation-1/client-generated-chat"
        CannedFeedURLProtocol.handlers[path] = (NativeChatTestFixtures.errorData, 400)
        let provider = MutableRetryDraftProvider()
        let session = NativeChatMCPAgentSession(service: PendingTurnMCPService())
        await session.useAccount("member-a")
        try await session.connect()
        let viewModel = try NativeChatViewModel(
            client: makeClient(), routeMatch: nil,
            titleProviderResolver: RetryDraftProviderResolver(endpointID: UUID(), provider: provider),
            mcpSession: session, currentMemberId: "member-a"
        )
        viewModel.selectedConversationId = "conversation-1"
        viewModel.selectTitleProvider(.appleFoundationModels)
        viewModel.draftMessage = "A tool-free answer"

        await viewModel.sendDraftMessage()
        XCTAssertTrue(provider.receivedMcpSnapshot)
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertNil(viewModel.pendingLocalTurn?.mcpSnapshot)
        XCTAssertEqual(viewModel.draftMessage, "A tool-free answer")

        try await session.refreshTools()
        provider.available = false
        CannedFeedURLProtocol.handlers[path] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 2)
        XCTAssertNil(viewModel.pendingLocalTurn)
    }

    func testAdmittedAppleMCPCallKeepsAccountBoundPendingTurn() async throws {
        let path = "/api/v1/conversations/conversation-1/client-generated-chat"
        CannedFeedURLProtocol.handlers[path] = (NativeChatTestFixtures.errorData, 400)
        let provider = MutableRetryDraftProvider()
        provider.admittedMCPCall = true
        let session = NativeChatMCPAgentSession(service: PendingTurnMCPService())
        await session.useAccount("member-a")
        try await session.connect()
        let viewModel = try NativeChatViewModel(
            client: makeClient(), routeMatch: nil,
            titleProviderResolver: RetryDraftProviderResolver(endpointID: UUID(), provider: provider),
            mcpSession: session, currentMemberId: "member-a"
        )
        viewModel.selectedConversationId = "conversation-1"
        viewModel.selectTitleProvider(.appleFoundationModels)
        viewModel.draftMessage = "Run a tool once"

        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertNotNil(viewModel.pendingLocalTurn?.mcpSnapshot)
        await session.useAccount("member-b")
        provider.available = false
        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
    }
}

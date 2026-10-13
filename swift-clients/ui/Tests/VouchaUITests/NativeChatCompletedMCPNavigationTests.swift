import Foundation
import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatCompletedMCPNavigationTests: NativeRouteSurfaceViewModelTestCase {
    func testLeavingConversationDiscardsRestoredPromptWithCompletedMcpTurn() async throws {
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
        viewModel.draftMessage = "Mutate once"
        await viewModel.sendDraftMessage()
        XCTAssertEqual(viewModel.draftMessage, "Mutate once")
        XCTAssertNotNil(viewModel.pendingLocalTurn?.mcpSnapshot)
        XCTAssertEqual(provider.generationCount, 1)

        await viewModel.selectConversation(id: nil)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.draftMessage, "")
        viewModel.selectedConversationId = "conversation-1"
        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
    }

    func testEditedRestoredPromptIsDiscardedWithCompletedMcpTurn() async throws {
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
        viewModel.draftMessage = "Mutate once"
        await viewModel.sendDraftMessage()
        XCTAssertNotNil(viewModel.pendingLocalTurn?.mcpSnapshot)
        viewModel.draftMessage = "Mutate once, edited"

        await viewModel.selectConversation(id: nil)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.draftMessage, "")
        viewModel.selectedConversationId = "conversation-1"
        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
    }

    func testMountedMemberChangeClearsFormerMembersCompletedMcpTurn() async throws {
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
        viewModel.draftMessage = "Mutate once"
        await viewModel.sendDraftMessage()
        XCTAssertNotNil(viewModel.pendingLocalTurn?.mcpSnapshot)
        XCTAssertEqual(provider.generationCount, 1)

        await session.useAccount("member-b")
        viewModel.currentMemberId = "member-b"
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.draftMessage, "")
        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertTrue(viewModel.messages.isEmpty)
        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
    }

    func testSessionAccountChangeBeforeMountedIdentityUpdateCannotRegenerateCompletedTurn() async throws {
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
        viewModel.draftMessage = "Mutate once"
        await viewModel.sendDraftMessage()
        XCTAssertNotNil(viewModel.pendingLocalTurn?.mcpSnapshot)

        await session.useAccount("member-b")
        await viewModel.sendDraftMessage()
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.draftMessage, "")
        await session.useAccount("member-a")
        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
    }

    func testMemberChangeDuringInitialListLoadCannotSelectFormerRoute() async throws {
        let listPath = "/api/v1/my/conversations"
        CannedFeedURLProtocol.handlers[listPath] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: listPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: listPath) }
        let barrier = CannedFeedURLProtocol.requestBarrier(path: listPath, method: "GET")
        let client = try makeClient()
        let viewModel = NativeChatViewModel(
            client: client,
            routeMatch: NativeRouteMatch(
                path: "/app/chat/conversation-1", template: "/app/chat/:id",
                params: ["id": "conversation-1"]
            ),
            currentMemberId: "member-a"
        )
        let initialLoad = Task { await viewModel.load() }
        _ = try await barrier.wait()

        viewModel.currentMemberId = "member-b"
        CannedFeedURLProtocol.releaseResponse(path: listPath)
        await initialLoad.value
        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.map(\.url.path), [listPath])
    }
}

import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatPendingTurnReviewTests: NativeRouteSurfaceViewModelTestCase {
    func testTitleGenerationDoesNotHoldTheNextSend() async throws {
        let path = "/api/v1/conversations/conversation-3/client-generated-chat"
        CannedFeedURLProtocol.handlers["/api/v1/conversations"] = (NativeChatTestFixtures.createdConversationData, 201)
        CannedFeedURLProtocol.handlers[path] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        let titleHold = HeldTitleGeneration()
        let provider = MutableRetryDraftProvider()
        provider.titleHold = titleHold
        let viewModel = try NativeChatViewModel(
            client: makeClient(), routeMatch: nil,
            titleProviderResolver: RetryDraftProviderResolver(endpointID: UUID(), provider: provider)
        )
        viewModel.draftMessage = "Hello"

        await viewModel.sendDraftMessage()
        await titleHold.waitForStart()
        XCTAssertFalse(viewModel.isSendingDraft)
        viewModel.draftMessage = "Next message"
        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 2)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 2)
        await titleHold.release()
    }

    func testReloadContainingBothPendingIdsClearsTheRetry() throws {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.selectedConversationId = "conversation-1"
        viewModel.pendingLocalTurn = .init(
            context: .init(
                conversationId: "conversation-1", text: "Hello", userMessageId: "pending-user",
                assistantMessageId: "pending-assistant", createdConversation: false
            ),
            response: .init(content: "Reply", modelProvider: "apple_foundation", modelName: nil)
        )
        viewModel.activeConversationDetailLoadRevision = 1
        let response = try NativeChatTestFixtures.decode(ChatMessagesResponse.self, """
        {"results":[
          {"id":"pending-user","conversation_id":"conversation-1","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"user","content":"Hello","error":null}},
          {"id":"pending-assistant","conversation_id":"conversation-1","created_at":"2026-01-01T00:00:01Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:01Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":"Reply","error":null}}
        ],"page_info":{"has_next_page":false,"start_cursor":"pending-user","end_cursor":null}}
        """)

        viewModel.applyConversationDetailResponse(response, id: "conversation-1", revision: 1)

        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.messages.map(\.id), ["pending-user", "pending-assistant"])
    }

    func testChangedModelStartsNewTurnButUnavailableProviderCanRetryPersistence() async throws {
        let path = "/api/v1/conversations/conversation-1/client-generated-chat"
        CannedFeedURLProtocol.handlers[path] = (NativeChatTestFixtures.errorData, 400)
        let provider = MutableRetryDraftProvider()
        let resolver = RetryDraftProviderResolver(endpointID: UUID(), provider: provider)
        let viewModel = try NativeChatViewModel(client: makeClient(), routeMatch: nil, titleProviderResolver: resolver)
        viewModel.selectedConversationId = "conversation-1"
        viewModel.draftMessage = "Hello"

        await viewModel.sendDraftMessage()
        let first = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == path })
        XCTAssertEqual(provider.generationCount, 1)

        provider.model = "model-b"
        await viewModel.sendDraftMessage()
        let second = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == path })
        XCTAssertNotEqual(first.body, second.body)
        XCTAssertEqual(provider.generationCount, 2)

        provider.available = false
        await viewModel.sendDraftMessage()
        let third = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == path })
        let secondPayload = try payload(second)
        let thirdPayload = try payload(third)
        XCTAssertEqual(secondPayload, thirdPayload)
        XCTAssertEqual(provider.generationCount, 2)

        provider.model = "model-c"
        await viewModel.sendDraftMessage()
        XCTAssertEqual(provider.generationCount, 2)
        provider.model = "model-b"
        provider.available = true
        await viewModel.sendDraftMessage()
        let afterRestoringOldModel = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == path })
        let restoredPayload = try payload(afterRestoringOldModel)
        XCTAssertNotEqual(restoredPayload, secondPayload)
        XCTAssertEqual(provider.generationCount, 3)
    }

    func testFailedMCPPersistenceCannotRetryAfterAccountSwitch() async throws {
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
        viewModel.draftMessage = "Hello"

        await viewModel.sendDraftMessage()
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
        XCTAssertNotNil(viewModel.pendingLocalTurn?.mcpSnapshot)

        await session.useAccount("member-b")
        provider.available = false
        await viewModel.sendDraftMessage()
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
        XCTAssertEqual(provider.generationCount, 1)
    }

    func testFailedMCPPersistenceRetriesCompletedTurnAfterSameMemberRefreshAndProviderChange() async throws {
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
        viewModel.draftMessage = "Run once"
        await viewModel.sendDraftMessage()
        let first = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == path })
        XCTAssertNotNil(viewModel.pendingLocalTurn?.mcpSnapshot)
        XCTAssertEqual(provider.generationCount, 1)

        try await session.refreshTools()
        provider.model = "model-b"
        viewModel.selectTitleProvider(.appleFoundationModels)
        viewModel.draftMessage = "Run once, edited"
        viewModel.draftMessage = "Run once"
        CannedFeedURLProtocol.handlers[path] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        await viewModel.sendDraftMessage()
        let requests = CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }
        XCTAssertEqual(requests.count, 2)
        let firstBody = try XCTUnwrap(first.body?.data(using: .utf8))
        let retriedBody = try XCTUnwrap(requests.last?.body?.data(using: .utf8))
        let firstPayload = try XCTUnwrap(JSONSerialization.jsonObject(with: firstBody) as? NSDictionary)
        let retriedPayload = try XCTUnwrap(JSONSerialization.jsonObject(with: retriedBody) as? NSDictionary)
        XCTAssertEqual(retriedPayload, firstPayload)
        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.messages.map(\.role), [.user, .assistant])
    }

    func testPossibleToolSideEffectNeverRestoresDraftForOrdinaryRetry() async throws {
        let provider = MutableRetryDraftProvider()
        provider.mcpFailure = LocalLLMToolLoopFailure.partialExecution
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

        XCTAssertTrue(provider.receivedMcpSnapshot)
        XCTAssertEqual(viewModel.draftMessage, "")
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.streamErrorMessage, .message(.sharedChatMcpPossibleSideEffect))
        XCTAssertFalse(CannedFeedURLProtocol.capturedRequests.contains {
            $0.url.path.hasSuffix("/client-generated-chat")
        })
    }

    func testMcpRefreshDuringGenerationRestoresSameMemberDraft() async throws {
        let hold = HeldTitleGeneration()
        let provider = MutableRetryDraftProvider()
        provider.mcpHold = hold
        let session = NativeChatMCPAgentSession(service: PendingTurnMCPService())
        await session.useAccount("member-a")
        try await session.connect()
        let viewModel = try NativeChatViewModel(
            client: makeClient(), routeMatch: nil,
            titleProviderResolver: RetryDraftProviderResolver(endpointID: UUID(), provider: provider),
            mcpSession: session, currentMemberId: "member-a"
        )
        viewModel.selectedConversationId = "conversation-1"
        viewModel.draftMessage = "Keep this prompt"

        let send = Task { await viewModel.sendDraftMessage() }
        await hold.waitForStart()
        try await session.refreshTools()
        await hold.release()
        await send.value

        XCTAssertEqual(viewModel.draftMessage, "Keep this prompt")
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertFalse(CannedFeedURLProtocol.capturedRequests.contains {
            $0.url.path.hasSuffix("/client-generated-chat")
        })
    }

    func testMcpAccountSwitchDuringGenerationDoesNotRestoreOldMemberDraft() async throws {
        let hold = HeldTitleGeneration()
        let provider = MutableRetryDraftProvider()
        provider.mcpHold = hold
        let session = NativeChatMCPAgentSession(service: PendingTurnMCPService())
        await session.useAccount("member-a")
        try await session.connect()
        let viewModel = try NativeChatViewModel(
            client: makeClient(), routeMatch: nil,
            titleProviderResolver: RetryDraftProviderResolver(endpointID: UUID(), provider: provider),
            mcpSession: session, currentMemberId: "member-a"
        )
        viewModel.selectedConversationId = "conversation-1"
        viewModel.draftMessage = "Old member prompt"

        let send = Task { await viewModel.sendDraftMessage() }
        await hold.waitForStart()
        viewModel.currentMemberId = "member-b"
        await session.useAccount("member-b")
        await hold.release()
        await send.value

        XCTAssertEqual(viewModel.draftMessage, "")
        XCTAssertTrue(viewModel.messages.isEmpty)
    }

    func testMcpSnapshotIsNotGivenToNewMemberBeforeSessionSwitchFinishes() async throws {
        let path = "/api/v1/conversations/conversation-1/client-generated-chat"
        CannedFeedURLProtocol.handlers[path] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        let provider = MutableRetryDraftProvider()
        let session = NativeChatMCPAgentSession(service: PendingTurnMCPService())
        await session.useAccount("member-a")
        try await session.connect()
        let viewModel = try NativeChatViewModel(
            client: makeClient(), routeMatch: nil,
            titleProviderResolver: RetryDraftProviderResolver(endpointID: UUID(), provider: provider),
            mcpSession: session, currentMemberId: "member-b"
        )
        viewModel.selectedConversationId = "conversation-1"
        viewModel.draftMessage = "New member prompt"

        await viewModel.sendDraftMessage()

        XCTAssertEqual(provider.generationCount, 1)
        XCTAssertFalse(provider.receivedMcpSnapshot)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
    }

    func testSameMemberRefreshAfterPersistenceAcknowledgementKeepsServerTurnVisible() async throws {
        let path = "/api/v1/conversations/conversation-1/client-generated-chat"
        CannedFeedURLProtocol.handlers[path] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
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
        viewModel.draftMessage = "Persist this"

        let send = Task { await viewModel.sendDraftMessage() }
        _ = try await barrier.wait()
        XCTAssertTrue(provider.receivedMcpSnapshot)
        try await session.refreshTools()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await send.value

        XCTAssertEqual(viewModel.draftMessage, "")
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.messages.map(\.role), [.user, .assistant])
        XCTAssertTrue(viewModel.messages.contains { $0.content == "Local assistant answer" })
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
    }

    func testTypingNextDraftDuringHeldPersistencePreservesTheNewDraftAfterFailure() async throws {
        let path = "/api/v1/conversations/conversation-1/client-generated-chat"
        CannedFeedURLProtocol.handlers[path] = (NativeChatTestFixtures.errorData, 400)
        let provider = MutableRetryDraftProvider()
        let resolver = RetryDraftProviderResolver(endpointID: UUID(), provider: provider)
        let viewModel = try NativeChatViewModel(client: makeClient(), routeMatch: nil, titleProviderResolver: resolver)
        viewModel.selectedConversationId = "conversation-1"
        viewModel.draftMessage = "Hello"

        let requestBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        CannedFeedURLProtocol.suspendResponse(path: path)
        let send = Task { await viewModel.sendDraftMessage() }
        let first: CannedFeedURLProtocol.CapturedRequest
        do {
            first = try await requestBarrier.wait(timeout: .seconds(10))
            viewModel.draftMessage = "Next draft"
            CannedFeedURLProtocol.releaseResponse(path: path)
            await send.value
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await send.value
            throw error
        }

        XCTAssertEqual(viewModel.draftMessage, "Next draft")
        XCTAssertNotNil(viewModel.pendingLocalTurn)
        await viewModel.sendDraftMessage()
        let next = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == path })
        let firstPayload = try payload(first)
        let nextPayload = try payload(next)
        XCTAssertNotEqual(nextPayload, firstPayload)
        XCTAssertEqual(provider.generationCount, 2)
    }

    func testChangingProviderDuringHeldPersistenceKeepsTheSubmittedTurnForRetry() async throws {
        let path = "/api/v1/conversations/conversation-1/client-generated-chat"
        CannedFeedURLProtocol.handlers[path] = (NativeChatTestFixtures.errorData, 400)
        let provider = MutableRetryDraftProvider()
        let resolver = RetryDraftProviderResolver(endpointID: UUID(), provider: provider)
        let viewModel = try NativeChatViewModel(client: makeClient(), routeMatch: nil, titleProviderResolver: resolver)
        viewModel.selectedConversationId = "conversation-1"
        viewModel.draftMessage = "Hello"

        let requestBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        CannedFeedURLProtocol.suspendResponse(path: path)
        let send = Task { await viewModel.sendDraftMessage() }
        let first: CannedFeedURLProtocol.CapturedRequest
        do {
            first = try await requestBarrier.wait(timeout: .seconds(10))
            let originalSelection = viewModel.titleProviderSelection
            viewModel.selectTitleProvider(.appleFoundationModels)
            XCTAssertEqual(viewModel.titleProviderSelection, originalSelection)
            CannedFeedURLProtocol.releaseResponse(path: path)
            await send.value
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await send.value
            throw error
        }

        await viewModel.sendDraftMessage()
        let retried = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == path })
        let firstPayload = try payload(first)
        let retriedPayload = try payload(retried)
        XCTAssertEqual(retriedPayload, firstPayload)
        XCTAssertEqual(provider.generationCount, 1)
    }

    func testChangingProviderDuringHeldFirstConversationCreateKeepsTheSubmittedTurnForRetry() async throws {
        let createPath = "/api/v1/conversations"
        let persistPath = "/api/v1/conversations/conversation-3/client-generated-chat"
        CannedFeedURLProtocol.handlers[createPath] = (NativeChatTestFixtures.createdConversationData, 201)
        CannedFeedURLProtocol.handlers[persistPath] = (NativeChatTestFixtures.errorData, 400)
        let provider = MutableRetryDraftProvider()
        let resolver = RetryDraftProviderResolver(endpointID: UUID(), provider: provider)
        let viewModel = try NativeChatViewModel(client: makeClient(), routeMatch: nil, titleProviderResolver: resolver)
        viewModel.draftMessage = "Hello"
        let createRequest = CannedFeedURLProtocol.requestBarrier(path: createPath, method: "POST")
        CannedFeedURLProtocol.suspendResponse(path: createPath)
        let send = Task { await viewModel.sendDraftMessage() }
        do {
            _ = try await createRequest.wait(timeout: .seconds(10))
            XCTAssertTrue(viewModel.isSendingDraft)
            let originalSelection = viewModel.titleProviderSelection
            viewModel.selectTitleProvider(.appleFoundationModels)
            XCTAssertEqual(viewModel.titleProviderSelection, originalSelection)
            CannedFeedURLProtocol.releaseResponse(path: createPath)
            await send.value
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: createPath)
            await send.value
            throw error
        }
        XCTAssertFalse(viewModel.isSendingDraft)
        let first = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == persistPath })
        await viewModel.sendDraftMessage()
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == persistPath }.count, 2)
        let retried = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == persistPath })
        let firstPayload = try payload(first)
        let retriedPayload = try payload(retried)
        XCTAssertEqual(retriedPayload, firstPayload)
        XCTAssertEqual(provider.generationCount, 1)
    }
}

@MainActor
final class PendingTurnMCPService: NativeChatMCPServicing {
    func connect(accountId _: String) async throws -> DecodedJSONValue {
        .object(["tools": .array([])])
    }

    func refreshTools(accountId _: String) async throws -> DecodedJSONValue {
        .object(["tools": .array([])])
    }

    func disconnect(accountId _: String) async throws {}
}

private func payload(_ request: CannedFeedURLProtocol.CapturedRequest) throws -> NSDictionary {
    let data = try XCTUnwrap(request.body?.data(using: .utf8))
    return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? NSDictionary)
}

actor HeldTitleGeneration {
    private var started = false
    private var startWaiter: CheckedContinuation<Void, Never>?
    private var releaseWaiter: CheckedContinuation<Void, Never>?

    func waitUntilReleased() async {
        started = true
        startWaiter?.resume()
        startWaiter = nil
        await withCheckedContinuation { releaseWaiter = $0 }
    }

    func waitForStart() async {
        if started { return }
        await withCheckedContinuation { startWaiter = $0 }
    }

    func release() {
        releaseWaiter?.resume()
        releaseWaiter = nil
    }
}

struct RetryDraftProviderResolver: NativeChatTitleProviderResolving {
    let endpointID: UUID
    let provider: MutableRetryDraftProvider

    func defaultSelection() -> NativeChatTitleProviderKind {
        .openAICompatible(endpointID: endpointID)
    }

    func provider(for _: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        provider
    }
}

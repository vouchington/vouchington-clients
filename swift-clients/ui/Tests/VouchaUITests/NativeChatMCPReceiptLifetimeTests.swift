import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPReceiptLifetimeTests: NativeRouteSurfaceViewModelTestCase {
    private let persistencePath = "/api/v1/conversations/conversation-1/client-generated-chat"

    func testReceiptPublicationRejectsWrongConversationAndMissingToolIdentity() async throws {
        let provider = ReceiptTurnProvider { turn in
            let receipt = Self.receipt(turn)
            let otherConversation = NativeChatMCPTurnOwner(
                accountId: turn.owner.accountId, revision: turn.owner.revision,
                conversationId: "another-conversation", userMessageId: turn.owner.userMessageId
            )
            XCTAssertFalse(turn.publish(.init(
                id: UUID(), owner: otherConversation, toolName: receipt.toolName,
                envelope: receipt.envelope, kind: receipt.kind
            )))
            XCTAssertFalse(turn.publish(.init(
                id: UUID(), owner: turn.owner, toolName: "", envelope: receipt.envelope, kind: receipt.kind
            )))
            XCTAssertTrue(turn.publish(receipt))
            return .toolResultsOnly(.contextStopped)
        }
        let viewModel = try await viewModel(provider)
        viewModel.draftMessage = "Read one fixture"
        await viewModel.sendDraftMessage()
        let receipts = try XCTUnwrap(viewModel.mcpReceiptLedger?.receipts)
        XCTAssertEqual(receipts.count, 1)
        XCTAssertEqual(receipts.first?.toolName, "read_fixture")
        XCTAssertEqual(persistedRequests, 0)
    }

    func testStoppedResultIsLocalWithoutAssistantPersistenceAndExpiresWithNextTurn() async throws {
        let provider = ReceiptTurnProvider { turn in
            XCTAssertTrue(turn.publish(Self.receipt(turn)))
            return .toolResultsOnly(.contextStopped)
        }
        let viewModel = try await viewModel(provider)
        viewModel.draftMessage = "Read one fixture"
        await viewModel.sendDraftMessage()
        let first = try XCTUnwrap(viewModel.mcpReceiptLedger)
        XCTAssertEqual(first.receipts.count, 1)
        XCTAssertEqual(first.continuation, .contextStopped)
        XCTAssertEqual(viewModel.messages.filter { $0.role == .user }.count, 1)
        XCTAssertTrue(viewModel.messages.filter { $0.role == .assistant }.isEmpty)
        XCTAssertEqual(viewModel.draftMessage, "")
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertEqual(persistedRequests, 0)

        viewModel.draftMessage = "Read a new fixture"
        await viewModel.sendDraftMessage()
        let next = try XCTUnwrap(viewModel.mcpReceiptLedger)
        XCTAssertNotEqual(next.owner.userMessageId, first.owner.userMessageId)
        XCTAssertFalse(viewModel.messages.contains { $0.id == first.owner.userMessageId })
        XCTAssertEqual(next.receipts.count, 1)
        let calls = await provider.calls
        XCTAssertEqual(calls, 2)
        XCTAssertEqual(persistedRequests, 0)

        viewModel.selectedConversationId = nil
        XCTAssertNil(viewModel.mcpReceiptLedger)
        XCTAssertTrue(viewModel.messages.isEmpty)
    }

    func testHistoryRefreshClearsTemporaryReceiptAndUserOverlay() async throws {
        let provider = ReceiptTurnProvider { turn in
            XCTAssertTrue(turn.publish(Self.receipt(turn)))
            return .toolResultsOnly(.contextStopped)
        }
        let viewModel = try await viewModel(provider)
        viewModel.draftMessage = "Read temporary fixture"
        await viewModel.sendDraftMessage()
        XCTAssertNotNil(viewModel.mcpReceiptLedger)
        XCTAssertEqual(viewModel.messages.count, 1)
        viewModel.activeConversationDetailLoadRevision = 7
        let response = try NativeChatTestFixtures.decode(ChatMessagesResponse.self, """
        {"results":[],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
        """)
        viewModel.applyConversationDetailResponse(response, id: "conversation-1", revision: 7)
        XCTAssertNil(viewModel.mcpReceiptLedger)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertEqual(persistedRequests, 0)
        let calls = await provider.calls
        XCTAssertEqual(calls, 1)

        viewModel.draftMessage = "Read another temporary fixture"
        await viewModel.sendDraftMessage()
        XCTAssertNotNil(viewModel.mcpReceiptLedger)
        viewModel.activeConversationDetailLoadRevision = 8
        viewModel.applyConversationDetailError("synthetic history failure", id: "conversation-1", revision: 8)
        XCTAssertNil(viewModel.mcpReceiptLedger)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNotNil(viewModel.detailErrorMessage)
        XCTAssertEqual(persistedRequests, 0)
    }

    func testCancellationAfterPublicationKeepsKnownReceiptButAdmitsNoLateReceipt() async throws {
        let receiptPublished = expectation(description: "receipt published before cancellation")
        let held = HeldTitleGeneration()
        let provider = ReceiptTurnProvider { turn in
            XCTAssertTrue(turn.publish(Self.receipt(turn)))
            receiptPublished.fulfill()
            await held.waitUntilReleased()
            XCTAssertFalse(turn.publish(Self.receipt(turn)))
            return .toolResultsOnly(.processingStopped)
        }
        let viewModel = try await viewModel(provider)
        viewModel.draftMessage = "Read fixture then stop"
        let sending = Task { await viewModel.sendDraftMessage() }
        await fulfillment(of: [receiptPublished], timeout: 5)
        viewModel.abortStreaming()
        await held.release()
        await sending.value
        XCTAssertEqual(viewModel.mcpReceiptLedger?.receipts.count, 1)
        XCTAssertEqual(viewModel.mcpReceiptLedger?.continuation, .cancelled)
        XCTAssertEqual(viewModel.messages.filter { $0.role == .user }.count, 1)
        XCTAssertTrue(viewModel.messages.filter { $0.role == .assistant }.isEmpty)
        XCTAssertEqual(persistedRequests, 0)
    }

    func testCancellationBeforePublicationDiscardsLateResultAndLocalUserTurn() async throws {
        let waiting = expectation(description: "waiting before receipt")
        let held = HeldTitleGeneration()
        let provider = ReceiptTurnProvider { turn in
            waiting.fulfill()
            await held.waitUntilReleased()
            XCTAssertFalse(turn.publish(Self.receipt(turn)))
            return .toolResultsOnly(.contextStopped)
        }
        let viewModel = try await viewModel(provider)
        viewModel.draftMessage = "Read fixture then cancel early"
        let sending = Task { await viewModel.sendDraftMessage() }
        await fulfillment(of: [waiting], timeout: 5)
        viewModel.abortStreaming()
        await held.release()
        await sending.value
        XCTAssertTrue(viewModel.mcpReceiptLedger?.receipts.isEmpty == true)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertEqual(persistedRequests, 0)
    }

    func testAccountRevisionChangeRejectsLateReceiptAndClearsPriorState() async throws {
        let session = NativeChatMCPAgentSession(service: PendingTurnMCPService())
        await session.useAccount("member-a")
        try await session.connect()
        let provider = ReceiptTurnProvider { turn in
            await session.useAccount("member-b")
            XCTAssertFalse(turn.publish(Self.receipt(turn)))
            return .toolResultsOnly(.contextStopped)
        }
        let viewModel = try await viewModel(provider, session: session)
        viewModel.draftMessage = "Read fixture for member-a"
        await viewModel.sendDraftMessage()
        XCTAssertNil(viewModel.mcpReceiptLedger)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(persistedRequests, 0)
    }

    func testKnownReceiptDoesNotHideLaterAmbiguousRpcOrRestoreResendDraft() async throws {
        let provider = ReceiptTurnProvider { turn in
            XCTAssertTrue(turn.publish(Self.receipt(turn)))
            throw LocalLLMToolLoopFailure.partialExecution
        }
        let viewModel = try await viewModel(provider)
        viewModel.draftMessage = "Run once, then unknown second result"
        await viewModel.sendDraftMessage()
        XCTAssertEqual(viewModel.mcpReceiptLedger?.receipts.count, 1)
        XCTAssertEqual(viewModel.mcpReceiptLedger?.continuation, .ambiguous)
        XCTAssertEqual(viewModel.streamErrorMessage, .message(.sharedChatMcpPossibleSideEffect))
        XCTAssertEqual(viewModel.draftMessage, "")
        XCTAssertNil(viewModel.pendingLocalTurn)
        await viewModel.sendDraftMessage()
        let calls = await provider.calls
        XCTAssertEqual(calls, 1)
        XCTAssertEqual(persistedRequests, 0)
    }

    func testPersistenceRetryKeepsOriginalAnswerAndReceiptsWithoutRegeneration() async throws {
        CannedFeedURLProtocol.handlers[persistencePath] = (NativeChatTestFixtures.errorData, 400)
        let provider = ReceiptTurnProvider { turn in
            XCTAssertTrue(turn.publish(Self.receipt(turn)))
            return .answer(.init(
                content: "Original model answer",
                modelProvider: "apple_foundation",
                modelName: "fixture",
                admittedMCPCall: true
            ))
        }
        let viewModel = try await viewModel(provider)
        viewModel.draftMessage = "Read and answer once"
        await viewModel.sendDraftMessage()
        let receipt = try XCTUnwrap(viewModel.mcpReceiptLedger?.receipts.first)
        XCTAssertEqual(viewModel.pendingLocalTurn?.response.content, "Original model answer")
        XCTAssertEqual(viewModel.draftMessage, "Read and answer once")
        XCTAssertEqual(persistedRequests, 1)

        CannedFeedURLProtocol.handlers[persistencePath] = (NativeChatTitleProviderTests.clientGeneratedChatData, 200)
        await viewModel.sendDraftMessage()
        let calls = await provider.calls
        XCTAssertEqual(calls, 1)
        XCTAssertEqual(persistedRequests, 2)
        XCTAssertEqual(viewModel.mcpReceiptLedger?.receipts.first, receipt)
        XCTAssertNil(viewModel.mcpReceiptLedger?.temporaryUserMessageId)
        XCTAssertNil(viewModel.pendingLocalTurn)
        XCTAssertEqual(viewModel.messages.filter { $0.role == .user }.count, 1)
    }

    private var persistedRequests: Int {
        CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == persistencePath }.count
    }

    private func viewModel(
        _ provider: ReceiptTurnProvider, session supplied: NativeChatMCPAgentSession? = nil
    ) async throws -> NativeChatViewModel {
        let session = supplied ?? NativeChatMCPAgentSession(service: PendingTurnMCPService())
        if supplied == nil {
            await session.useAccount("member-a")
            try await session.connect()
        }
        let viewModel = try NativeChatViewModel(
            client: makeClient(), routeMatch: nil,
            titleProviderResolver: StubTitleProviderResolver(
                defaultSelectionValue: .appleFoundationModels,
                providers: [.appleFoundationModels: provider]
            ),
            mcpSession: session, currentMemberId: "member-a"
        )
        viewModel.selectedConversationId = "conversation-1"
        return viewModel
    }

    private static func receipt(_ turn: NativeChatMCPTurnContext) -> NativeChatMCPReceipt {
        .init(
            id: UUID(),
            owner: turn.owner,
            toolName: "read_fixture",
            envelope: #"{"content":[],"structuredContent":{"success":true},"isError":false}"#,
            kind: .response
        )
    }
}

private actor ReceiptTurnProvider: NativeChatTitleProviding {
    typealias Body = @MainActor @Sendable (NativeChatMCPTurnContext) async throws -> NativeChatMCPGenerationOutcome?
    let body: Body
    private(set) var calls = 0
    init(body: @escaping Body) {
        self.body = body
    }

    nonisolated var status: NativeChatTitleProviderStatus {
        .init(isAvailable: true, detail: nil)
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        nil
    }

    func generateAssistantResponse(
        to _: String, history _: [NativeChatTimelineMessage],
        mcpSession _: NativeChatMCPAgentSession?, mcpSnapshot _: NativeChatMCPAgentSession.ConnectedSnapshot?,
        mcpTurn: NativeChatMCPTurnContext?
    ) async throws -> NativeChatMCPGenerationOutcome? {
        calls += 1
        guard let mcpTurn else { return nil }
        return try await body(mcpTurn)
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        nil
    }
}

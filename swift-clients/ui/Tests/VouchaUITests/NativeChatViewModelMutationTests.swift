import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatViewModelMutationTests: NativeRouteSurfaceViewModelTestCase {
    func testChatViewModelStreamsHostedDraftMessage() async {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")
        let events = AsyncThrowingStream<ChatStreamEvent, Error> { continuation in
            continuation.yield(.metadata(.init(
                conversationId: "conversation-1",
                userMessageId: "user-1",
                assistantMessageId: "assistant-1",
                jobId: "job-1"
            )))
            continuation.yield(.text("Hello"))
            continuation.yield(.done)
            continuation.finish()
        }

        await viewModel.streamHostedDraftMessage(
            events: events,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false,
                providerSelection: .anthropic
            )
        )

        XCTAssertNil(viewModel.streamErrorMessage)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Hi", "Hello"])
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testChatViewModelSendsHostedProviderUpgradeRequest() async throws {
        CannedFeedURLProtocol.contentTypes["/api/v1/conversations/conversation-1/chat"] = "text/event-stream"
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-1/chat"] = (
            Data("event: done\ndata: {}\n\n".utf8),
            200
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.titleProviderSelection = .anthropic
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")

        await viewModel.streamHostedDraftMessage(
            client: client,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false,
                providerSelection: .anthropic
            )
        )

        XCTAssertNil(viewModel.streamErrorMessage)
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/conversations/conversation-1/chat")
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedBodies.first??.contains(#""provider":"anthropic""#),
            true
        )
    }

    func testChatViewModelSurfacesHostedVouchaErrors() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-1/chat"] = (
            NativeChatTestFixtures.errorData,
            500
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.titleProviderSelection = .openAI
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")

        await viewModel.streamHostedDraftMessage(
            client: client,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false
            )
        )

        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("An error occurred."))
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Hi"])
    }

    func testChatViewModelSurfacesHostedStreamVouchaErrors() async {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")
        let events = AsyncThrowingStream<ChatStreamEvent, Error> { continuation in
            continuation.finish(throwing: VouchaError.apiMessage(
                statusCode: 400,
                preconditionCode: nil,
                message: "Hosted failed"
            ))
        }

        await viewModel.streamHostedDraftMessage(
            events: events,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false
            )
        )

        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("Hosted failed"))
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testChatViewModelSurfacesHostedStreamGenericErrors() async {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")
        let events = AsyncThrowingStream<ChatStreamEvent, Error> { continuation in
            continuation.finish(throwing: ChatMutationTestError(message: "Transport failed"))
        }

        await viewModel.streamHostedDraftMessage(
            events: events,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false
            )
        )

        XCTAssertEqual(viewModel.streamErrorMessage, .message(.nativeSwiftChatUnableToSendMessage))
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testChatViewModelSurfacesMissingLocalResponse() async throws {
        let client = try makeClient()
        let provider = LocalDraftProvider(
            status: .init(isAvailable: true, detail: .verbatim("No local answer.")),
            response: nil
        )
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")

        await viewModel.generateLocalDraftMessage(
            client: client,
            provider: provider,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false
            )
        )

        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("No local answer."))
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertTrue(viewModel.messages.isEmpty)
    }

    func testChatViewModelRollsBackNewConversationLocalFailure() async throws {
        let client = try makeClient()
        let provider = LocalDraftProvider(
            status: .init(isAvailable: true, detail: .verbatim("No local answer.")),
            response: nil
        )
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")

        await viewModel.generateLocalDraftMessage(
            client: client,
            provider: provider,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: true
            )
        )

        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("No local answer."))
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNil(viewModel.streamingAssistantMessageId)
    }

    func testChatViewModelSurfacesLocalProviderGenericErrors() async throws {
        let client = try makeClient()
        let provider = LocalDraftProvider(
            status: .init(isAvailable: true, detail: nil),
            response: "unused",
            error: ChatMutationTestError(message: "Local failed")
        )
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user", role: .user, content: "Hi", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")

        await viewModel.generateLocalDraftMessage(
            client: client,
            provider: provider,
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false
            )
        )

        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("Local failed"))
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertTrue(viewModel.messages.isEmpty)
    }

    func testChatViewModelIgnoresStaleFoundationModelsGenerationAfterUserMessageAdvances() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-1/client-generated-chat"] = (
            Data("{}".utf8),
            200
        )
        let capturedURLCount = CannedFeedURLProtocol.capturedURLs.count
        let capturedMethodCount = CannedFeedURLProtocol.capturedMethods.count
        let capturedBodyCount = CannedFeedURLProtocol.capturedBodies.count

        let client = try makeClient()
        let provider = DelayedLocalDraftProvider()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user-old", role: .user, content: "First", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user-old")

        let staleGeneration = Task { @MainActor in
            await viewModel.generateLocalDraftMessage(
                client: client,
                provider: provider,
                context: .init(
                    conversationId: "conversation-1",
                    text: "First",
                    userMessageId: "local-user-old",
                    createdConversation: false
                )
            )
        }

        await provider.waitUntilAssistantResponseRequested()
        viewModel.messages.append(.init(
            id: "local-user-new",
            role: .user,
            content: "Second",
            isStreaming: false
        ))
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user-new")

        await provider.resumeAssistantResponse(returning: "stale")
        await staleGeneration.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, capturedURLCount)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.count, capturedMethodCount)
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.count, capturedBodyCount)
        XCTAssertEqual(viewModel.streamingConversationId, "conversation-1")
        XCTAssertEqual(viewModel.streamingUserMessageId, "local-user-new")
        XCTAssertNil(viewModel.streamingAssistantMessageId)
        XCTAssertEqual(viewModel.messages.count, 1)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Second"])
        XCTAssertEqual(viewModel.messages.first?.role, .user)
        XCTAssertEqual(viewModel.streamedContent, "")
        XCTAssertTrue(viewModel.isStreaming)
    }

    func testChatViewModelDoesNotDeleteHostedUserWhenPreviousLocalAssistantIdIsStale() {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.messages = [
            .init(id: "hosted-user", role: .user, content: "Hosted", isStreaming: false),
            .init(id: "local-assistant-stale", role: .assistant, content: "", isStreaming: false)
        ]
        viewModel.streamingAssistantMessageId = "local-assistant-stale"

        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "hosted-user")
        viewModel.abortStreaming()

        XCTAssertEqual(viewModel.messages.map(\.id), ["hosted-user", "local-assistant-stale"])
        XCTAssertNil(viewModel.streamingUserMessageId)
        XCTAssertNil(viewModel.streamingAssistantMessageId)
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testChatViewModelCancelsStreamingBeforeDeletingConversation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1"] = (Data("{}".utf8), 204)
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.selectedConversationId = "conversation-1"
        viewModel.streamingConversationId = "conversation-1"
        viewModel.streamingAssistantMessageId = "assistant-local"
        viewModel.isStreaming = true
        viewModel.streamedContent = "partial"
        viewModel.messages = [
            .init(id: "assistant-local", role: .assistant, content: "partial", isStreaming: true)
        ]
        viewModel.streamTask = Task {
            try? await Task.sleep(nanoseconds: 5_000_000_000)
        }

        await viewModel.deleteSelectedConversation()

        XCTAssertNil(viewModel.streamTask)
        XCTAssertNil(viewModel.streamingConversationId)
        XCTAssertNil(viewModel.streamingAssistantMessageId)
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["DELETE"])
    }

    func testChatViewModelIgnoresStaleSameConversationStreamCallbacksAfterResend() async {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.messages = [
            .init(id: "local-user-new", role: .user, content: "Second", isStreaming: false)
        ]

        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user-new")
        await viewModel.applyStreamEvent(
            .metadata(
                .init(
                    conversationId: "conversation-1",
                    userMessageId: "user-message-1",
                    assistantMessageId: "assistant-new",
                    jobId: "job-1"
                )
            ),
            conversationId: "conversation-1",
            userMessageId: "local-user-new"
        )
        await viewModel.applyStreamEvent(
            .text("fresh"),
            conversationId: "conversation-1",
            userMessageId: "local-user-new"
        )

        XCTAssertTrue(viewModel.isStreaming)
        XCTAssertEqual(viewModel.messages.count, 2)
        XCTAssertEqual(viewModel.messages.last?.content, "fresh")

        await viewModel.applyStreamEvent(
            .text("stale"),
            conversationId: "conversation-1",
            userMessageId: "local-user-old"
        )
        await viewModel.finishStream(
            conversationId: "conversation-1",
            userMessageId: "local-user-old",
            createdConversation: false
        )

        XCTAssertTrue(viewModel.isStreaming)
        XCTAssertEqual(viewModel.messages.count, 2)
        XCTAssertEqual(viewModel.messages.last?.content, "fresh")
        XCTAssertEqual(viewModel.streamingConversationId, "conversation-1")
        XCTAssertEqual(viewModel.streamingUserMessageId, "local-user-new")
        XCTAssertEqual(viewModel.streamingAssistantMessageId, "assistant-new")

        await viewModel.finishStream(
            conversationId: "conversation-1",
            userMessageId: "local-user-new",
            createdConversation: false
        )

        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertNil(viewModel.streamingConversationId)
        XCTAssertNil(viewModel.streamingUserMessageId)
        XCTAssertNil(viewModel.streamingAssistantMessageId)
    }
}

private struct ChatMutationTestError: LocalizedError {
    let message: String

    var errorDescription: String? {
        message
    }
}

private struct LocalDraftProvider: NativeChatTitleProviding {
    let status: NativeChatTitleProviderStatus
    let response: String?
    let error: Error?

    init(
        status: NativeChatTitleProviderStatus,
        response: String?,
        error: Error? = nil
    ) {
        self.status = status
        self.response = response
        self.error = error
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        if let error {
            throw error
        }
        return response.map {
            NativeChatAssistantResponse(
                content: $0,
                modelProvider: "openai_compatible",
                modelName: "test-local-model"
            )
        }
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        nil
    }
}

private actor DelayedLocalDraftProvider: NativeChatTitleProviding {
    nonisolated let status = NativeChatTitleProviderStatus(isAvailable: true, detail: nil)

    private let gate = DelayedLocalDraftGate()

    func waitUntilAssistantResponseRequested() async {
        await gate.waitUntilAssistantResponseRequested()
    }

    func resumeAssistantResponse(returning response: String?) async {
        await gate.resumeAssistantResponse(returning: response)
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        await gate.generateAssistantResponse()
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        nil
    }
}

private actor DelayedLocalDraftGate {
    private var started = false
    private var startContinuation: CheckedContinuation<Void, Never>?
    private var responseContinuation: CheckedContinuation<NativeChatAssistantResponse?, Never>?

    func waitUntilAssistantResponseRequested() async {
        if started {
            return
        }
        await withCheckedContinuation { continuation in
            startContinuation = continuation
        }
    }

    func generateAssistantResponse() async -> NativeChatAssistantResponse? {
        await withCheckedContinuation { continuation in
            responseContinuation = continuation
            started = true
            startContinuation?.resume()
            startContinuation = nil
        }
    }

    func resumeAssistantResponse(returning response: String?) {
        responseContinuation?.resume(returning: response.map {
            NativeChatAssistantResponse(content: $0, modelProvider: "openai_compatible", modelName: "test-local-model")
        })
        responseContinuation = nil
    }
}

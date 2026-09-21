import Foundation
@testable import VouchaFeatures
import VouchaLocalization
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatViewModelLocalGenerationTests: NativeRouteSurfaceViewModelTestCase {
    func testChatViewModelKeepsLocalAssistantContentHiddenUntilPersistenceSucceeds() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-1/client-generated-chat"] = (
            NativeChatTestFixtures.errorData,
            400
        )

        let client = try makeClient()
        let provider = CapturingLocalPersistenceDraftProvider(
            status: .init(isAvailable: true, detail: nil),
            response: "unsafe output"
        )
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.messages = [
            .init(id: "previous-user", role: .user, content: "Earlier", isStreaming: false),
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

        XCTAssertNotNil(viewModel.streamErrorMessage)
        XCTAssertEqual(viewModel.draftMessage, "Hi")
        XCTAssertEqual(viewModel.messages.map(\.id), ["previous-user"])
        XCTAssertNil(viewModel.streamingUserMessageId)
        XCTAssertNil(viewModel.streamingAssistantMessageId)
        XCTAssertEqual(provider.capturedHistory.map(\.id), ["previous-user"])
    }

    func testChatViewModelIgnoresStaleLocalUnavailableRollbackWhenNewStreamIsActive() async throws {
        let client = try makeClient()
        let resolver = UnavailableLocalDraftProviderResolver(
            provider: CapturingLocalPersistenceDraftProvider(
                status: .init(isAvailable: false, detail: .verbatim("On-device chat is unavailable.")),
                response: nil
            )
        )
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.messages = [
            .init(id: "stale-user", role: .user, content: "Old", isStreaming: false),
            .init(id: "current-user", role: .user, content: "New", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "current-user")
        viewModel.ensureAssistantMessage(id: "current-assistant")
        viewModel.streamedContent = "Streaming"
        viewModel.updateStreamingAssistantMessage()

        await viewModel.streamDraftMessage(
            client: client,
            context: .init(
                conversationId: "conversation-1",
                text: "Old",
                userMessageId: "stale-user",
                createdConversation: false,
                providerSelection: .appleFoundationModels
            )
        )

        XCTAssertEqual(viewModel.streamingConversationId, "conversation-1")
        XCTAssertEqual(viewModel.streamingUserMessageId, "current-user")
        XCTAssertEqual(viewModel.streamingAssistantMessageId, "current-assistant")
        XCTAssertTrue(viewModel.isStreaming)
        XCTAssertEqual(viewModel.streamedContent, "Streaming")
        XCTAssertNil(viewModel.streamErrorMessage)
        XCTAssertEqual(viewModel.messages.map(\.id), ["current-user", "current-assistant"])
    }

    func testChatViewModelDoesNotCreateAssistantForStaleLocalGeneration() async throws {
        let client = try makeClient()
        let provider = CapturingLocalPersistenceDraftProvider(
            status: .init(isAvailable: true, detail: nil),
            response: "stale response"
        )
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.messages = [
            .init(id: "stale-user", role: .user, content: "Old", isStreaming: false),
            .init(id: "current-user", role: .user, content: "New", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "current-user")

        await viewModel.generateLocalDraftMessage(
            client: client,
            provider: provider,
            context: .init(
                conversationId: "conversation-1",
                text: "Old",
                userMessageId: "stale-user",
                createdConversation: false
            )
        )

        XCTAssertEqual(provider.generateAssistantResponseCallCount, 0)
        XCTAssertNil(viewModel.streamingAssistantMessageId)
        XCTAssertEqual(viewModel.messages.map(\.id), ["current-user"])
        XCTAssertTrue(viewModel.isStreaming)
    }

    func testChatViewModelGracefullyFailsLocalGenerationWhenAndroidAICoreIsSelected() async throws {
        let client = try makeClient()
        let resolver = UnavailableLocalDraftProviderResolver(provider: NativeChatAndroidAICoreProvider())
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.messages = [.init(id: "local-user", role: .user, content: "Hi", isStreaming: false)]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "local-user")

        await viewModel.generateLocalDraftMessage(
            client: client,
            provider: resolver.provider(for: .androidAICore),
            context: .init(
                conversationId: "conversation-1",
                text: "Hi",
                userMessageId: "local-user",
                createdConversation: false,
                providerSelection: .androidAICore
            )
        )

        XCTAssertEqual(
            viewModel.streamErrorMessage,
            .message(.nativeSwiftRouteSurfaceProviderUnavailable, parameters: ["provider": "android_aicore"])
        )
        XCTAssertEqual(viewModel.draftMessage, "Hi")
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNil(viewModel.streamingUserMessageId)
        XCTAssertNil(viewModel.streamingAssistantMessageId)
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testChatViewModelExcludesAbortedLocalAssistantPlaceholderFromNextLocalHistory() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-1/client-generated-chat"] = (
            Self.clientGeneratedChatData,
            200
        )

        let client = try makeClient()
        let provider = BlockingLocalPersistenceDraftProvider(
            firstResponse: "aborted response",
            secondResponse: "fresh response"
        )
        let resolver = UnavailableLocalDraftProviderResolver(provider: provider)
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.selectedConversationId = "conversation-1"
        viewModel.messages = [
            .init(id: "previous-user", role: .user, content: "Earlier", isStreaming: false),
            .init(id: "first-user", role: .user, content: "First", isStreaming: false)
        ]
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "first-user")

        let firstTurn = Task { @MainActor in
            await viewModel.generateLocalDraftMessage(
                client: client,
                provider: provider,
                context: .init(
                    conversationId: "conversation-1",
                    text: "First",
                    userMessageId: "first-user",
                    createdConversation: false
                )
            )
        }
        await provider.waitUntilFirstAssistantResponseRequested()
        XCTAssertEqual(viewModel.messages.count, 3)
        XCTAssertEqual(viewModel.messages.prefix(2).map(\.id), ["previous-user", "first-user"])
        XCTAssertEqual(viewModel.messages.last?.role, .assistant)
        XCTAssertTrue(viewModel.messages.last?.isStreaming == true)
        viewModel.abortStreaming()
        XCTAssertEqual(viewModel.messages.map(\.id), ["previous-user"])

        viewModel.messages.append(.init(id: "second-user", role: .user, content: "Second", isStreaming: false))
        viewModel.beginStreaming(conversationId: "conversation-1", userMessageId: "second-user")

        let secondTurn = Task { @MainActor in
            await viewModel.generateLocalDraftMessage(
                client: client,
                provider: provider,
                context: .init(
                    conversationId: "conversation-1",
                    text: "Second",
                    userMessageId: "second-user",
                    createdConversation: false
                )
            )
        }
        await provider.waitUntilSecondAssistantResponseRequested()

        let secondHistory = await provider.capturedSecondHistory
        XCTAssertEqual(secondHistory.map(\.id), ["previous-user"])

        await provider.resumeFirstAssistantResponse()
        await firstTurn.value
        await secondTurn.value

        XCTAssertEqual(viewModel.messages.map(\.id), [
            "previous-user",
            "persisted-user-2",
            "persisted-assistant-2"
        ])
        XCTAssertNil(viewModel.streamingAssistantMessageId)
        XCTAssertFalse(viewModel.messages.contains { $0.role == .assistant && $0.isStreaming })
    }
}

private extension NativeChatViewModelLocalGenerationTests {
    static let clientGeneratedChatData = Data(
        """
        {
          "user_message": {
            "id": "persisted-user-2",
            "conversation_id": "conversation-1",
            "created_at": "2026-01-01T00:00:00Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:00:00Z",
            "updated_by_id": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "content": {
              "role": "user",
              "content": "Second"
            }
          },
          "assistant_message": {
            "id": "persisted-assistant-2",
            "conversation_id": "conversation-1",
            "created_at": "2026-01-01T00:00:01Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:00:01Z",
            "updated_by_id": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "content": {
              "role": "assistant",
              "content": "Fresh response"
            }
          },
          "agentic_run": {
            "id": "run-2",
            "conversation_id": "conversation-1",
            "conversation_message_id": "persisted-assistant-2",
            "parent_agentic_run_id": null,
            "model_name": "apple-foundation-system",
            "model_provider": "apple_foundation",
            "input": {"message": "Second"},
            "output": {"response": "Fresh response"},
            "error": null,
            "status": "completed",
            "termination_reason": "no_tool_calls",
            "started_at": "2026-01-01T00:00:01Z",
            "completed_at": "2026-01-01T00:00:01Z",
            "failed_at": null,
            "created_at": "2026-01-01T00:00:01Z",
            "updated_at": "2026-01-01T00:00:01Z",
            "deleted_at": null
          }
        }
        """.utf8
    )
}

private final class CapturingLocalPersistenceDraftProvider: NativeChatTitleProviding, @unchecked Sendable {
    let status: NativeChatTitleProviderStatus
    let response: String?
    private(set) var capturedHistory: [NativeChatTimelineMessage] = []
    private(set) var generateAssistantResponseCallCount = 0

    init(status: NativeChatTitleProviderStatus, response: String?) {
        self.status = status
        self.response = response
    }

    func generateAssistantResponse(
        to _: String,
        history: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        generateAssistantResponseCallCount += 1
        capturedHistory = history
        return response.map {
            NativeChatAssistantResponse(
                content: $0,
                modelProvider: "apple_foundation",
                modelName: "apple-foundation-system"
            )
        }
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        nil
    }
}

private struct UnavailableLocalDraftProviderResolver: NativeChatTitleProviderResolving, @unchecked Sendable {
    let provider: any NativeChatTitleProviding

    func defaultSelection() -> NativeChatTitleProviderKind {
        .appleFoundationModels
    }

    func provider(for _: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        provider
    }
}

private actor BlockingLocalPersistenceDraftProvider: NativeChatTitleProviding {
    nonisolated let status = NativeChatTitleProviderStatus(isAvailable: true, detail: nil)

    private let firstResponse: String?
    private let secondResponse: String?
    private var callCount = 0
    private var firstRequestContinuation: CheckedContinuation<Void, Never>?
    private var firstResponseContinuation: CheckedContinuation<NativeChatAssistantResponse?, Never>?
    private var secondRequestContinuation: CheckedContinuation<Void, Never>?

    private(set) var capturedSecondHistory: [NativeChatTimelineMessage] = []

    init(firstResponse: String?, secondResponse: String?) {
        self.firstResponse = firstResponse
        self.secondResponse = secondResponse
    }

    func waitUntilFirstAssistantResponseRequested() async {
        if callCount > 0 {
            return
        }
        await withCheckedContinuation { continuation in
            firstRequestContinuation = continuation
        }
    }

    func waitUntilSecondAssistantResponseRequested() async {
        if capturedSecondHistory.isEmpty == false {
            return
        }
        await withCheckedContinuation { continuation in
            secondRequestContinuation = continuation
        }
    }

    func resumeFirstAssistantResponse() {
        firstResponseContinuation?.resume(returning: firstResponse.map {
            NativeChatAssistantResponse(
                content: $0,
                modelProvider: "apple_foundation",
                modelName: "apple-foundation-system"
            )
        })
        firstResponseContinuation = nil
    }

    func generateAssistantResponse(
        to _: String,
        history: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        callCount += 1
        switch callCount {
        case 1:
            firstRequestContinuation?.resume()
            firstRequestContinuation = nil
            return await withCheckedContinuation { continuation in
                firstResponseContinuation = continuation
            }
        case 2:
            capturedSecondHistory = history
            secondRequestContinuation?.resume()
            secondRequestContinuation = nil
            return secondResponse.map {
                NativeChatAssistantResponse(
                    content: $0,
                    modelProvider: "apple_foundation",
                    modelName: "apple-foundation-system"
                )
            }
        default:
            return nil
        }
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        nil
    }
}

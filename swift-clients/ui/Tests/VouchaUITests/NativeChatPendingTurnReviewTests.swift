import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatPendingTurnReviewTests: NativeRouteSurfaceViewModelTestCase {
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

    func testTypingNextDraftDuringHeldPersistenceKeepsTheSubmittedTurnForRetry() async throws {
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

        XCTAssertEqual(viewModel.draftMessage, "Hello")
        await viewModel.sendDraftMessage()
        let retried = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.url.path == path })
        let firstPayload = try payload(first)
        let retriedPayload = try payload(retried)
        XCTAssertEqual(retriedPayload, firstPayload)
        XCTAssertEqual(provider.generationCount, 1)
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

private func payload(_ request: CannedFeedURLProtocol.CapturedRequest) throws -> NSDictionary {
    let data = try XCTUnwrap(request.body?.data(using: .utf8))
    return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? NSDictionary)
}

private final class MutableRetryDraftProvider: NativeChatTitleProviding, @unchecked Sendable {
    var model = "model-a"
    var available = true
    private(set) var generationCount = 0

    var status: NativeChatTitleProviderStatus {
        .init(isAvailable: available, detail: nil)
    }

    var retryModelIdentity: String? {
        model
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        generationCount += 1
        return .init(content: "Reply \(generationCount)", modelProvider: "openai_compatible", modelName: model)
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        nil
    }
}

private struct RetryDraftProviderResolver: NativeChatTitleProviderResolving {
    let endpointID: UUID
    let provider: MutableRetryDraftProvider

    func defaultSelection() -> NativeChatTitleProviderKind {
        .openAICompatible(endpointID: endpointID)
    }

    func provider(for _: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        provider
    }
}

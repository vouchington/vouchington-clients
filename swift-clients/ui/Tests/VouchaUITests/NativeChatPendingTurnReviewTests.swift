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
        let viewModel = try NativeChatViewModel(
            client: makeClient(), routeMatch: nil, titleProviderResolver: resolver
        )
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
        let secondPayload = try XCTUnwrap(JSONSerialization.jsonObject(
            with: XCTUnwrap(second.body?.data(using: .utf8))
        ) as? NSDictionary)
        let thirdPayload = try XCTUnwrap(JSONSerialization.jsonObject(
            with: XCTUnwrap(third.body?.data(using: .utf8))
        ) as? NSDictionary)
        XCTAssertEqual(secondPayload, thirdPayload)
        XCTAssertEqual(provider.generationCount, 2)
    }
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

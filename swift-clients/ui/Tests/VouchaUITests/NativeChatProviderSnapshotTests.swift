import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatProviderSnapshotTests: NativeRouteSurfaceViewModelTestCase {
    func testUnavailableLocalProviderDoesNotCreateConversationOrSendRequest() async throws {
        let resolver = NativeChatLiveTitleProviderResolver(
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "false"])
        )
        let viewModel = try NativeChatViewModel(client: makeClient(), routeMatch: nil, titleProviderResolver: resolver)
        viewModel.draftMessage = "Use a local model"
        viewModel.detailErrorMessage = .verbatim("Earlier detail failure")

        await viewModel.sendDraftMessage()

        XCTAssertEqual(viewModel.draftMessage, "Use a local model")
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNotNil(viewModel.streamErrorMessage)
        XCTAssertNil(viewModel.detailErrorMessage)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }
}

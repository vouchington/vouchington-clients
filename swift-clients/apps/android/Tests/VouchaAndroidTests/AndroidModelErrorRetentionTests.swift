import Foundation
@testable import VouchaAndroid
import VouchaCore
import VouchaLocalization
import XCTest

@MainActor
final class AndroidModelErrorRetentionTests: XCTestCase {
    func testStartupModelFailureSurvivesAccountTranscriptLoad() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FailingStatusRuntime())
        await viewModel.refreshStatus()
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidModelUnavailable)
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")

        XCTAssertEqual(viewModel.modelState, .unavailable)
        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertEqual(viewModel.chatErrorKind, .model)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidModelUnavailable)
        await viewModel.downloadModel()
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(AndroidTranscriptTestFactory.newConversationID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        await viewModel.loadConversationMessages(reset: true)
        XCTAssertEqual(viewModel.modelState, .available)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.chatErrorMessageKey)
    }
}

private struct FailingStatusRuntime: AndroidAICoreRunning {
    func status() async throws -> AndroidModelState {
        throw StatusFailure()
    }

    func download() async throws -> AndroidModelState {
        .available
    }

    func generate(prompt _: String) async throws -> String {
        "Unused"
    }
}

private struct StatusFailure: Error {}

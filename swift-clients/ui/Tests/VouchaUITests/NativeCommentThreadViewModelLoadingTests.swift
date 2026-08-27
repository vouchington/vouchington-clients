import Foundation
import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeCommentThreadViewModelLoadingTests: XCTestCase {
    func testIsLoadingCoversEachLoadState() {
        let viewModel = NativeCommentThreadViewModel(
            client: nil,
            rootPostId: "root-1",
            currentUserId: "user-1"
        )

        XCTAssertFalse(viewModel.isLoading)

        viewModel.threadState = .loading
        XCTAssertTrue(viewModel.isLoading)

        viewModel.threadState = .idle
        viewModel.ancestorState = .loading
        XCTAssertTrue(viewModel.isLoading)

        viewModel.ancestorState = .idle
        viewModel.focusedState = .loading
        XCTAssertTrue(viewModel.isLoading)
    }
}

import Observation
import SwiftUI
import ViewInspector
@testable import VouchaDesignSystem
import XCTest

@MainActor
final class HybridPaginationRenderedTests: XCTestCase {
    func testVisibleControlContinuesThroughShortAndEmptyPagesUntilTerminal() async throws {
        let model = PaginationProbe()
        let view = PaginationProbeView(model: model)
        try await ViewHosting.host(view) {
            for page in 0 ..< 3 {
                await fulfillment(of: [model.renderedLoading[page]], timeout: 3)
                XCTAssertEqual(model.calls, page + 1)
                XCTAssertTrue(try view.inspect().find(button: "Loading more").isDisabled())
                model.complete(terminal: page == 2)
            }
            await fulfillment(of: [model.finished], timeout: 3)
            XCTAssertEqual(model.calls, 3)
            XCTAssertEqual(model.itemCount, 1)
            XCTAssertThrowsError(try view.inspect().find(ViewType.Button.self))
        }
    }

    func testVisibleControlLeavesFailureForManualRetry() async throws {
        let model = PaginationProbe()
        let view = PaginationProbeView(model: model)
        try await ViewHosting.host(view) {
            await fulfillment(of: [model.renderedLoading[0]], timeout: 3)
            model.complete(terminal: false, failed: true)
            await fulfillment(of: [model.finished], timeout: 3)
            XCTAssertEqual(model.calls, 1)
            try view.inspect().find(button: "Try Again").tap()
            await fulfillment(of: [model.renderedLoading[1]], timeout: 3)
            XCTAssertEqual(model.calls, 2)
            model.complete(terminal: true)
        }
    }
}

@MainActor
@Observable
private final class PaginationProbe {
    var hasMore = true
    var isLoading = false
    var hasError = false
    var calls = 0
    var itemCount = 0
    let renderedLoading = (0 ..< 3).map { XCTestExpectation(description: "Rendered loading page \($0)") }
    let finished = XCTestExpectation(description: "Rendered terminal or error")
    private var response: CheckedContinuation<Void, Never>?

    func load() async {
        calls += 1
        isLoading = true
        hasError = false
        await withCheckedContinuation { response = $0 }
    }

    func complete(terminal: Bool, failed: Bool = false) {
        if calls == 1, !failed { itemCount = 1 }
        hasMore = !terminal
        hasError = failed
        isLoading = false
        response?.resume()
        response = nil
    }
}

private struct PaginationProbeView: View {
    let model: PaginationProbe

    var body: some View {
        ScrollView {
            VStack {
                ForEach(0 ..< model.itemCount, id: \.self) { _ in
                    Color.clear.frame(height: 20)
                }
                HybridPaginationControl(
                    hasMore: model.hasMore,
                    isLoading: model.isLoading,
                    hasError: model.hasError,
                    loadNextPage: model.load
                )
            }
        }
        .frame(width: 400, height: 400)
        .onChange(of: model.isLoading) { _, loading in
            if loading {
                model.renderedLoading[model.calls - 1].fulfill()
            } else if !model.hasMore || model.hasError {
                model.finished.fulfill()
            }
        }
    }
}

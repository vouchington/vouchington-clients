import SwiftUI
import ViewInspector
@testable import VouchaDesignSystem
import XCTest

@MainActor
final class HybridPaginationControlTests: XCTestCase {
    func testAutomaticLoadRunsOnceForEachVisibleEdge() {
        var state = AutomaticPaginationVisibilityState()

        XCTAssertFalse(state.shouldLoad(isVisible: false, hasMore: true, isLoading: false, hasError: false))
        XCTAssertTrue(state.shouldLoad(isVisible: true, hasMore: true, isLoading: false, hasError: false))
        XCTAssertFalse(state.shouldLoad(isVisible: true, hasMore: true, isLoading: false, hasError: false))
        XCTAssertFalse(state.shouldLoad(isVisible: false, hasMore: true, isLoading: false, hasError: false))
        XCTAssertTrue(state.shouldLoad(isVisible: true, hasMore: true, isLoading: false, hasError: false))
    }

    func testAutomaticLoadContinuesAfterLoadingCompletesWithoutANewVisibleEdge() {
        var state = AutomaticPaginationVisibilityState()

        XCTAssertFalse(state.shouldLoad(isVisible: true, hasMore: true, isLoading: true, hasError: false))
        XCTAssertTrue(state.shouldLoad(hasMore: true, isLoading: false, hasError: false))
        XCTAssertFalse(state.shouldLoad(hasMore: true, isLoading: false, hasError: false))
    }

    func testCompletionDoesNotRetryErrorsOrLoadHiddenTerminalOrDisabledPages() {
        for (visible, hasMore, hasError, disabled) in [
            (true, true, true, false),
            (false, true, false, false),
            (true, false, false, false),
            (true, true, false, true)
        ] {
            var state = AutomaticPaginationVisibilityState()
            XCTAssertFalse(state.shouldLoad(isVisible: visible, hasMore: true, isLoading: true, hasError: false))
            XCTAssertFalse(state.shouldLoad(
                hasMore: hasMore, isLoading: false, hasError: hasError, isDisabled: disabled
            ))
        }
    }

    func testAutomaticLoadDoesNotStartWhenDisabled() {
        var state = AutomaticPaginationVisibilityState()

        XCTAssertFalse(state.shouldLoad(
            isVisible: true,
            hasMore: true,
            isLoading: false,
            hasError: false,
            isDisabled: true
        ))
    }

    func testShowsLoadMoreAndRunsSharedActionFromButton() async throws {
        var calls = 0
        let sut = HybridPaginationControl(hasMore: true, isLoading: false, hasError: false) {
            calls += 1
        }

        try sut.inspect().find(button: "Load more").tap()
        await Task.yield()

        XCTAssertEqual(calls, 1)
    }

    func testShowsLocalizedLoadingAndDisablesButton() throws {
        let sut = HybridPaginationControl(hasMore: true, isLoading: true, hasError: false) {}
        let button = try sut.inspect().find(button: "Loading more")

        XCTAssertTrue(button.isDisabled())
    }

    func testShowsRetryAfterFailure() throws {
        let sut = HybridPaginationControl(hasMore: false, isLoading: false, hasError: true) {}

        XCTAssertNoThrow(try sut.inspect().find(button: "Try Again"))
    }

    func testDisablesButtonWhenRequested() throws {
        let sut = HybridPaginationControl(
            hasMore: true,
            isLoading: false,
            hasError: false,
            isDisabled: true
        ) {}

        XCTAssertTrue(try sut.inspect().find(button: "Load more").isDisabled())
    }

    func testHidesWhenPageIsTerminal() throws {
        let sut = HybridPaginationControl(hasMore: false, isLoading: false, hasError: false) {}

        XCTAssertThrowsError(try sut.inspect().find(ViewType.Button.self))
    }
}

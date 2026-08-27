import Foundation
import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeAiCostsViewModelTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    func testLoadsCursorPagesInServerOrderAndPreservesRowsAfterContinuationFailure() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/admin/ai-costs"] = [
            (ApiFixtureLoader.data("native.admin-ai-costs.default"), 200, 0),
            (Data(#"{"error":"offline"}"#.utf8), 500, 0),
            (ApiFixtureLoader.data("native.admin-ai-costs.page-2"), 200, 0)
        ]
        let viewModel = try NativeAiCostsViewModel(client: makeClient())

        await viewModel.loadIfNeeded()
        let firstPageIds = viewModel.rows.map(\.id)
        await viewModel.loadNextPage()

        XCTAssertEqual(viewModel.rows.map(\.id), firstPageIds)
        XCTAssertNotNil(viewModel.pagination.lastError)
        await viewModel.loadNextPage()

        XCTAssertEqual(viewModel.rows.map(\.communitySlug), ["alpha-community", "beta-community", "gamma-community"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs[1].query?.contains("after="), true)
    }

    func testRefreshReplacesVisibleRowsOnlyAfterSuccessfulFirstPageResponse() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/admin/ai-costs"] = [
            (ApiFixtureLoader.data("native.admin-ai-costs.default"), 200, 0),
            (Data(#"{"error":"offline"}"#.utf8), 500, 0),
            (ApiFixtureLoader.data("native.admin-ai-costs.empty"), 200, 0)
        ]
        let viewModel = try NativeAiCostsViewModel(client: makeClient())

        await viewModel.loadIfNeeded()
        let firstPageIds = viewModel.rows.map(\.id)
        await viewModel.refresh()

        XCTAssertEqual(viewModel.rows.map(\.id), firstPageIds)
        XCTAssertNotNil(viewModel.refreshError)
        XCTAssertNil(viewModel.pagination.lastError)
        await viewModel.refresh()

        XCTAssertEqual(viewModel.rows, [])
        XCTAssertNil(viewModel.refreshError)
        XCTAssertFalse(viewModel.pagination.hasMore)
    }

    func testPopulatedSurfaceRendersExactLocalizedMetricsAndUnpricedAnnotation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/admin/ai-costs"] = (
            ApiFixtureLoader.data("native.admin-ai-costs.default"),
            200
        )
        let viewModel = try NativeAiCostsViewModel(client: makeClient())
        await viewModel.loadIfNeeded()
        let surface = try NativeAiCostsSurface(
            entry: entry(for: .engineeringAiCosts),
            client: nil,
            isAdministrator: true
        )
        let inspection = try surface.content(viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))
            .inspect()
        let expectedCost = try UiMessages.string(
            ScaledMoneyAggregateFormatter.format(
                XCTUnwrap(viewModel.rows.first).totalCost,
                locale: Locale(identifier: "en_US")
            ),
            locale: Locale(identifier: "en_US")
        )

        XCTAssertNoThrow(try inspection.find(text: "alpha-community"))
        XCTAssertEqual(expectedCost, "$1,234,567,890,123,456,789.012345")
        XCTAssertEqual(
            UiMessages.string(.extractedAiCostsPageTotalCostUsdEed03fbf, locale: Locale(identifier: "en_US")),
            "Total Cost (USD)"
        )
        XCTAssertEqual(
            UiMessages.plural(
                .extractedAiCostsPageUnpricedRequestCount,
                value: 42,
                locale: Locale(identifier: "en_US")
            ),
            "42 unpriced requests"
        )
        XCTAssertEqual(UiMessages.number(9_876_543_210, locale: Locale(identifier: "en_US")), "9,876,543,210")
    }

    func testEmptyAndRetainedRefreshErrorStatesRenderWithoutDroppingRows() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/admin/ai-costs"] = [
            (ApiFixtureLoader.data("native.admin-ai-costs.empty"), 200, 0),
            (ApiFixtureLoader.data("native.admin-ai-costs.default"), 200, 0),
            (Data(#"{"error":"offline"}"#.utf8), 500, 0)
        ]
        let viewModel = try NativeAiCostsViewModel(client: makeClient())
        let surface = try NativeAiCostsSurface(
            entry: entry(for: .engineeringAiCosts),
            client: nil,
            isAdministrator: true
        )

        await viewModel.loadIfNeeded()
        XCTAssertNoThrow(try surface.content(viewModel).inspect().find(text: "No AI usage recorded yet."))
        await viewModel.refresh()
        await viewModel.refresh()
        let retainedError = try surface.content(viewModel).inspect()

        XCTAssertNoThrow(try retainedError.find(text: "alpha-community"))
        XCTAssertNoThrow(try retainedError.find(ErrorStateView.self))
    }
}

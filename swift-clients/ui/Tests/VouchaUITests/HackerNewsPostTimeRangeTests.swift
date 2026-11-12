import Foundation
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class HackerNewsPostTimeRangeTests: NativeRouteSurfaceViewModelTestCase {
    private func registerPages() {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            Data(
                #"{"results":[],"posts":{},"posts_metrics":{},"post_elections":{},"page_info":{"has_next_page":true,"end_cursor":"next=="}}"#
                    .utf8
            ), 200
        )
    }

    func testPostToggleForcesYearDropsCursorAndRendersOnlyBoundedOptions() async throws {
        registerPages()
        let model = try PostsListViewModel(client: makeClient(), feedType: "all", timeRange: .lastWeek)
        await model.load()
        await model.loadNextPage()
        model.hasHackerNewsReferences = true
        await model.filterReloadTask?.value
        XCTAssertEqual(model.selectedTimeRange, .lastYear)
        XCTAssertEqual(query("time_range"), "1y")
        XCTAssertNil(query("after"))
        XCTAssertEqual(query("has_hacker_news_references"), "true")
        let picker = try PostsListView(viewModel: model).inspect().find(PostTimeRangePicker.self)
        XCTAssertThrowsError(try picker.find(text: "All time"))
        XCTAssertNoThrow(try picker.find(text: "Last 30 days"))
        await model.selectTimeRange(.last30Days)
        XCTAssertEqual(query("time_range"), "1m")
        await model.loadNextPage()
        XCTAssertEqual(query("time_range"), "1m")
        XCTAssertEqual(query("after"), "next==")
        model.hasHackerNewsReferences = false
        await model.filterReloadTask?.value
        XCTAssertEqual(model.selectedTimeRange, .last30Days)
        XCTAssertEqual(query("time_range"), "1m")
        XCTAssertNil(query("has_hacker_news_references"))
        XCTAssertNoThrow(try PostsListView(viewModel: model).inspect().find(PostTimeRangePicker.self)
            .find(text: "All time"))
        model.hasHackerNewsReferences = true
        await model.filterReloadTask?.value
        await model.selectTimeRange(.allTime)
        XCTAssertEqual(model.selectedTimeRange, .lastYear)
        XCTAssertEqual(query("time_range"), "1y")
    }

    func testRouteDeepLinksClampMissingAndAllButRetainExplicitWeekAndMonth() async throws {
        registerPages()
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/reviews"))
        for (raw, expected) in [(nil, "1y"), ("all", "1y"), ("1w", "1w"), ("1m", "1m")] {
            let model = try NativeRouteSurfaceViewModel(
                entry: route.entry, client: makeClient(), routeMatch: route.match,
                routeQuery: "has_hacker_news_references=true" + (raw.map { "&time_range=\($0)" } ?? "")
            )
            await model.load()
            XCTAssertEqual(query("time_range"), expected)
            _ = try await model.loadPostPage(client: makeClient(), after: "next==")
            XCTAssertEqual(query("time_range"), expected)
            XCTAssertEqual(query("after"), "next==")
            XCTAssertEqual(query("post_types"), "review")
        }
    }

    func testRouteRenderedToggleAndPickerPreserveBoundedStateInNavigation() async throws {
        registerPages()
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let model = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match,
            routeQuery: "time_range=1w&after=old"
        )
        let surface = NativeListSurface(viewModel: model)
        try surface.inspect().find(ViewType.Toggle.self).tap()
        await model.hackerNewsFilterTask?.value
        XCTAssertEqual(model.postTimeRange, .lastYear)
        XCTAssertEqual(query("time_range"), "1y")
        XCTAssertNil(query("after"))
        let picker = try surface.inspect().find(PostTimeRangePicker.self)
        XCTAssertThrowsError(try picker.find(text: "All time"))
        let path = model.postTimeRangePath(.last30Days)
        let components = try XCTUnwrap(URLComponents(string: path))
        XCTAssertEqual(components.queryItems?.first { $0.name == "time_range" }?.value, "1m")
        XCTAssertEqual(components.queryItems?.first { $0.name == "has_hacker_news_references" }?.value, "true")
        XCTAssertNil(components.queryItems?.first { $0.name == "after" })
        try surface.inspect().find(ViewType.Toggle.self).tap()
        await model.hackerNewsFilterTask?.value
        XCTAssertEqual(model.postTimeRange, .lastYear)
        XCTAssertNoThrow(try surface.inspect().find(PostTimeRangePicker.self).find(text: "All time"))
    }

    private func query(_ name: String) -> String? {
        CannedFeedURLProtocol.capturedURLs.last
            .flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?
            .queryItems?.first { $0.name == name }?.value
    }
}

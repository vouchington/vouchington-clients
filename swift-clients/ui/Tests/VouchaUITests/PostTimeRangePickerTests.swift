import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class PostTimeRangePickerTests: NativeRouteSurfaceViewModelTestCase {
    func testDropdownRendersEveryLocalizedOptionAndLabel() throws {
        var selected: PostTimeRange?
        let sut = PostTimeRangePicker(range: .last30Days) { selected = $0 }
        let picker = try sut.inspect().find(ViewType.Picker.self)
        XCTAssertEqual(try picker.labelView().text().string(), "Time range")
        XCTAssertEqual(try picker.selectedValue(PostTimeRange.self), .last30Days)
        for (range, label) in [
            (PostTimeRange.lastWeek, "Last 7 days"),
            (.last30Days, "Last 30 days"),
            (.lastYear, "Last year"),
            (.allTime, "All time")
        ] {
            XCTAssertNoThrow(try picker.find(text: label))
            try picker.select(value: range)
            XCTAssertEqual(selected, range)
        }
    }

    func testRestrictedOptionsExcludeAllAndUseSuppliedDefault() throws {
        let sut = PostTimeRangePicker(
            range: .allTime,
            options: [.lastWeek, .last30Days, .lastYear],
            defaultRange: .lastYear,
            onSelect: { _ in }
        )
        let picker = try sut.inspect().find(ViewType.Picker.self)
        XCTAssertEqual(try picker.selectedValue(PostTimeRange.self), .lastYear)
        XCTAssertThrowsError(try picker.find(text: "All time"))
    }

    func testDropdownUsesEnvironmentLocale() throws {
        let locale = Locale(identifier: "es")
        let sut = PostTimeRangePicker(range: .lastWeek, onSelect: { _ in }).environment(\.locale, locale)
        let picker = try sut.inspect().find(ViewType.Picker.self)
        XCTAssertEqual(
            try picker.labelView().text().string(),
            UiMessages.string(.extractedPostsPostTimeRangeSelectLabel, locale: locale)
        )
        for key in [
            UiMessageKey.extractedPostsPostTimeRangeSelectLast7Days,
            .extractedPostsPostTimeRangeSelectLast30Days,
            .extractedPostsPostTimeRangeSelectLastYear,
            .extractedPostsPostTimeRangeSelectAllTime
        ] {
            XCTAssertNoThrow(try picker.find(text: UiMessages.string(key, locale: locale)))
        }
    }

    func testBrowsePickerPreservesFiltersAndResetsCursorOnNavigation() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/reviews"))
        let model = NativeRouteSurfaceViewModel(
            entry: route.entry, client: nil, routeMatch: route.match,
            routeQuery: "time_range=1w&after=opaque%3D%3D&sort=new&query=hello"
        )
        var navigated: String?
        let picker = try NativeListSurface(viewModel: model) { navigated = $0 }
            .inspect().find(ViewType.Picker.self)
        XCTAssertEqual(try picker.selectedValue(PostTimeRange.self), .lastWeek)
        try picker.select(value: PostTimeRange.lastYear)
        let components = try XCTUnwrap(try URLComponents(string: XCTUnwrap(navigated)))
        XCTAssertEqual(components.path, "/reviews")
        XCTAssertEqual(components.queryItems?.first { $0.name == "time_range" }?.value, "1y")
        XCTAssertNil(components.queryItems?.first { $0.name == "after" })
        XCTAssertEqual(components.queryItems?.first { $0.name == "query" }?.value, "hello")
        try picker.select(value: PostTimeRange.last30Days)
        XCTAssertFalse(try XCTUnwrap(navigated).contains("time_range"))
    }

    func testProfilePickerRendersOnEmptyPostCollectionsAndNavigates() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/posts"))
        let model = NativeRouteSurfaceViewModel(
            entry: route.entry, client: nil, routeMatch: route.match,
            routeQuery: "time_range=all&after=old"
        )
        model.userProfile.scope = .posts(.all)
        var navigated: String?
        let surface = NativeUserProfileSurface(
            entry: route.entry, viewModel: model, isSignedIn: false,
            turnstileSiteKey: nil, showSignIn: {}, onNavigate: { navigated = $0 }
        )
        let picker = try surface.inspect().find(ViewType.Picker.self)
        XCTAssertEqual(try picker.selectedValue(PostTimeRange.self), .allTime)
        try picker.select(value: PostTimeRange.last30Days)
        XCTAssertEqual(navigated, "/user/alice/posts")
    }

    func testRestrictedOptionsFallbackToFirstWhenDefaultUnavailable() throws {
        let sut = PostTimeRangePicker(range: .allTime, options: [.lastYear], onSelect: { _ in })
        XCTAssertEqual(try sut.inspect().find(ViewType.Picker.self).selectedValue(PostTimeRange.self), .lastYear)
    }

    func testReusableFeedViewHidesDropdownWithoutCallerConfiguration() throws {
        let client = try makeClient()
        for feedType in ["any", "follow_users", "follow_topics"] {
            let model = PostsListViewModel(client: client, feedType: feedType)
            XCTAssertEqual(model.timeRange, .lastWeek)
            XCTAssertThrowsError(try PostsListView(viewModel: model).inspect().find(PostTimeRangePicker.self))
        }
        let ordinary = PostsListViewModel(client: client, feedType: "all")
        XCTAssertNoThrow(try PostsListView(viewModel: ordinary).inspect().find(PostTimeRangePicker.self))
    }

    func testFeedDefaultAndContinuationAreFixedToWeek() async throws {
        let path = "/api/v1/feeds/posts/any"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (
                Data(
                    #"{"results":[],"posts":{},"posts_metrics":{},"post_elections":{},"page_info":{"has_next_page":true,"end_cursor":"older=="}}"#
                        .utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"{"results":[],"posts":{},"posts_metrics":{},"post_elections":{},"page_info":{"has_next_page":false,"end_cursor":null}}"#
                        .utf8
                ),
                200,
                0
            )
        ]
        let model = try PostsListViewModel(client: makeClient())
        await model.load()
        await model.selectTimeRange(.allTime)
        await model.loadNextPage()
        let urls = CannedFeedURLProtocol.capturedURLs
        XCTAssertEqual(urls.count, 2)
        XCTAssertEqual(urls.map {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems?
                .first { $0.name == "time_range" }?.value
        }, ["1w", "1w"])
        XCTAssertEqual(
            try URLComponents(url: XCTUnwrap(urls.last), resolvingAgainstBaseURL: false)?
                .queryItems?.first { $0.name == "after" }?.value,
            "older=="
        )
    }

    func testFeedHasNoTimeRangeDropdown() throws {
        let model = try NativeRouteSurfaceViewModel(entry: entry(for: .feedPosts), client: nil)
        XCTAssertThrowsError(try NativeListSurface(viewModel: model).inspect().find(ViewType.Picker.self))
    }
}

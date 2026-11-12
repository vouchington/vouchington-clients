import Foundation
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeTopicPostTimeRangeTests: NativeRouteSurfaceViewModelTestCase {
    func testTopicCollectionsResolveSlugAndForwardTheirExactFiltersAndWindowOnEveryPage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/swift"] = (
            Data(#"{"topic":{"id":"topic-1","slug":"swift","name":"Swift"}}"#.utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            Data(#"{"results":[],"posts":{},"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8), 200
        )
        let client = try makeClient()
        for (suffix, key, postType) in [
            ("posts", "topics", nil),
            ("discussions", "topics", nil),
            ("reviews", "review_topic", "review"),
            ("data-points", "data_point_topic", "data_point")
        ] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/\(suffix)"))
            for (range, expected) in [("1w", "1w"), ("1m", "1m"), ("1y", "1y"), ("all", "all")] {
                let model = NativeRouteSurfaceViewModel(
                    entry: route.entry, client: client, routeMatch: route.match,
                    routeQuery: "time_range=\(range)"
                )
                await model.load()
                if case .loaded = model.state {} else { XCTFail("Topic post collection failed to load") }
                _ = try await model.loadPostPage(client: client, after: "older==")
                let urls = Array(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/posts" }.suffix(2))
                XCTAssertEqual(urls.count, 2)
                XCTAssertEqual(urls.map { query("time_range", in: $0) }, [expected, expected])
                XCTAssertEqual(urls.map { query(key, in: $0) }, ["topic-1", "topic-1"])
                XCTAssertEqual(query("post_types", in: urls.last), postType)
                XCTAssertNil(query("after", in: urls.first))
                XCTAssertEqual(query("after", in: urls.last), "older==")
                XCTAssertEqual(query("sort", in: urls.last), "new")
            }
        }
    }

    func testTopicListPublicPaginationUsesCursorAndDoesNotResolveSlugAgain() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/swift"] = (
            Data(#"{"topic":{"id":"topic-1","slug":"swift","name":"Swift"}}"#.utf8), 200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [
            (
                Data(#"{"results":[],"posts":{},"page_info":{"has_next_page":true,"end_cursor":"opaque=="}}"#.utf8),
                200,
                0
            ),
            (Data(#"{"results":[],"posts":{},"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8), 200, 0)
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/reviews"))
        let model = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match, routeQuery: "time_range=1y"
        )
        await model.load()
        XCTAssertTrue(model.forwardPagination.hasMore)
        await model.loadMoreForwardRows()
        XCTAssertFalse(model.forwardPagination.hasMore)
        let urls = CannedFeedURLProtocol.capturedURLs
        XCTAssertEqual(urls.filter { $0.path == "/api/v1/topics/swift" }.count, 1)
        let posts = urls.filter { $0.path == "/api/v1/posts" }
        XCTAssertEqual(posts.count, 2)
        XCTAssertEqual(query("after", in: posts.last), "opaque==")
        XCTAssertEqual(query("review_topic", in: posts.last), "topic-1")
        XCTAssertEqual(query("time_range", in: posts.last), "1y")
    }

    func testEveryRegisteredTopicPostCollectionRendersDropdownAndResetsCursor() throws {
        for suffix in ["posts", "discussions", "reviews", "data-points"] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/\(suffix)"))
            let model = NativeRouteSurfaceViewModel(
                entry: route.entry, client: nil, routeMatch: route.match,
                routeQuery: "time_range=all&after=old"
            )
            XCTAssertTrue(model.showsPostTimeRangePicker)
            var navigated: String?
            let picker = try NativeListSurface(viewModel: model) { navigated = $0 }
                .inspect().find(ViewType.Picker.self)
            XCTAssertEqual(try picker.selectedValue(PostTimeRange.self), .allTime)
            try picker.select(value: PostTimeRange.last30Days)
            XCTAssertEqual(navigated, "/topic/swift/\(suffix)")
        }
    }

    func testTypedTopicCollectionsUseTheSameNativeListPolicy() throws {
        for type in [
            "bank-account",
            "card",
            "instance",
            "referral-program",
            "rewards-program",
            "rewards-program-status",
            "source",
            "topic"
        ] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/\(type)/swift/posts"))
            let model = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
            XCTAssertEqual(model.topicPostCollection, "posts")
            XCTAssertTrue(model.showsPostTimeRangePicker)
        }
    }

    func testTopicRootAndOtherSubroutesKeepDetailSurface() throws {
        for path in ["/topic/swift", "/topic/swift/tags/publisher_type"] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            let model = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
            XCTAssertNil(model.topicPostCollection)
            XCTAssertFalse(model.showsPostTimeRangePicker)
        }
    }

    private func query(_ name: String, in url: URL?) -> String? {
        url.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?
            .queryItems?.first { $0.name == name }?.value
    }
}

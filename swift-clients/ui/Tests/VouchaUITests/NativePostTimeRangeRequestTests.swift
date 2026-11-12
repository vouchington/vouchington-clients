import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativePostTimeRangeRequestTests: NativeRouteSurfaceViewModelTestCase {
    func testFeedUsesExplicitWeekOnEveryPage() async throws {
        let path = "/api/v1/feeds/posts/any"
        registerEmptyPage(path: path)
        let client = try makeClient()
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .feedPosts), client: client)

        _ = try await viewModel.loadPostPage(client: client, after: nil)
        _ = try await viewModel.loadPostPage(client: client, after: "older==")

        let urls = CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }
        XCTAssertEqual(urls.count, 2)
        XCTAssertEqual(urls.map { query("time_range", url: $0) }, ["1w", "1w"])
        XCTAssertTrue(urls.allSatisfy { query("has_hacker_news_references", url: $0) == nil })
        XCTAssertEqual(query("after", url: urls.last), "older==")
    }

    func testOrdinaryBrowseSendsExplicitMonthlyDefaultWithCursor() async throws {
        let path = "/api/v1/posts"
        registerEmptyPage(path: path)
        let client = try makeClient()
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .postsBrowse), client: client)

        _ = try await viewModel.loadPostPage(client: client, after: "older==")

        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(query("time_range", url: url), "1m")
        XCTAssertEqual(query("sort", url: url), "hot")
        XCTAssertEqual(query("after", url: url), "older==")
    }

    func testBrowseWindowsReachEveryPageForAllPostTypes() async throws {
        let path = "/api/v1/posts"
        registerEmptyPage(path: path)
        let client = try makeClient()
        for routePath in ["/posts", "/reviews", "/discussions", "/data-points"] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: routePath))
            for (value, expected) in [("1w", "1w"), ("1m", "1m"), ("1y", "1y"), ("all", "all")] {
                let model = NativeRouteSurfaceViewModel(
                    entry: route.entry, client: client, routeMatch: route.match,
                    routeQuery: "time_range=\(value)"
                )
                XCTAssertTrue(model.showsPostTimeRangePicker)
                _ = try await model.loadPostPage(client: client, after: nil)
                _ = try await model.loadPostPage(client: client, after: "older==")
                let urls = Array(CannedFeedURLProtocol.capturedURLs.suffix(2))
                XCTAssertEqual(urls.map { query("time_range", url: $0) }, [expected, expected])
                XCTAssertNil(query("after", url: urls.first))
                XCTAssertEqual(query("after", url: urls.last), "older==")
            }
        }
    }

    func testProfilePostWindowsReachInitialAndContinuationRequests() async throws {
        registerEmptyPage(path: "/api/v1/posts")
        let client = try makeClient()
        for suffix in ["posts", "reviews", "discussions", "comments"] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/\(suffix)"))
            for range in ["1w", "1m", "1y", "all"] {
                let model = NativeRouteSurfaceViewModel(
                    entry: route.entry, client: client, routeMatch: route.match,
                    routeQuery: "time_range=\(range)"
                )
                XCTAssertTrue(model.showsPostTimeRangePicker)
                _ = try await model.userPostPage(userId: "alice", filter: .all, after: nil, client: client)
                _ = try await model.userPostPage(userId: "alice", filter: .all, after: "profile==", client: client)
                let urls = Array(CannedFeedURLProtocol.capturedURLs.suffix(2))
                let expected: String? = range
                XCTAssertEqual(urls.map { query("time_range", url: $0) }, [expected, expected])
                XCTAssertEqual(query("creator", url: urls.last), "alice")
                XCTAssertEqual(query("after", url: urls.last), "profile==")
            }
        }
    }

    func testInvalidRangeUsesOrdinaryDefault() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        for query in [nil, "time_range=invalid", "time_range=1d"] {
            let model = NativeRouteSurfaceViewModel(
                entry: route.entry, client: nil, routeMatch: route.match, routeQuery: query
            )
            XCTAssertEqual(model.postTimeRange, .last30Days)
            XCTAssertEqual(model.postTimeRangeQueryValue, "1m")
        }
    }

    private func registerEmptyPage(path: String) {
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"{"results":[],"posts":{},"posts_metrics":{},"post_elections":{},"page_info":{"has_next_page":false,"end_cursor":null}}"#
                    .utf8
            ),
            200
        )
    }

    private func query(_ name: String, url: URL?) -> String? {
        url.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?
            .queryItems?.first { $0.name == name }?.value
    }
}

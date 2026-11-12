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

    func testOrdinaryBrowseRetainsOmittedDefaultWithCursor() async throws {
        let path = "/api/v1/posts"
        registerEmptyPage(path: path)
        let client = try makeClient()
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .postsBrowse), client: client)

        _ = try await viewModel.loadPostPage(client: client, after: "older==")

        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertNil(query("time_range", url: url))
        XCTAssertEqual(query("sort", url: url), "hot")
        XCTAssertEqual(query("after", url: url), "older==")
    }

    private func registerEmptyPage(path: String) {
        CannedFeedURLProtocol.handlers[path] = (
            Data(#"{"results":[],"posts":{},"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8),
            200
        )
    }

    private func query(_ name: String, url: URL?) -> String? {
        url.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?
            .queryItems?.first { $0.name == name }?.value
    }
}

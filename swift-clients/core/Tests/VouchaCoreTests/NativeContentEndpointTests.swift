@testable import VouchaAPI
import XCTest

final class NativeContentEndpointTests: XCTestCase {
    func testPostsFeedForwardsOpaqueCursorAfterTheFilters() {
        let endpoint = Endpoint.postsFeed(
            postTypes: "link",
            timeRange: "1w",
            hasHackerNewsReferences: true,
            sort: "new",
            after: "opaque+/=cursor",
            limit: 10
        )

        XCTAssertEqual(endpoint.path, "/api/v1/posts")
        XCTAssertEqual(endpoint.queryItems, [
            .init(name: "limit", value: "10"),
            .init(name: "sort", value: "new"),
            .init(name: "post_types", value: "link"),
            .init(name: "time_range", value: "1w"),
            .init(name: "has_hacker_news_references", value: "true"),
            .init(name: "after", value: "opaque+/=cursor")
        ])
    }

    func testOrdinaryPostListDefaultsToMonthAndOmitsCursorAndOptionalFilters() {
        let endpoint = Endpoint.postsFeed()

        XCTAssertEqual(endpoint.queryItems, [
            .init(name: "limit", value: "25"),
            .init(name: "time_range", value: "1m")
        ])
        XCTAssertFalse(endpoint.queryItems.contains { $0.name == "after" })
    }

    func testPostsFeedEncodesFalseHackerNewsFilter() {
        let endpoint = Endpoint.postsFeed(hasHackerNewsReferences: false)

        XCTAssertTrue(endpoint.queryItems.contains(.init(name: "has_hacker_news_references", value: "false")))
    }
}

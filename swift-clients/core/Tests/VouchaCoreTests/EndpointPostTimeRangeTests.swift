import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointPostTimeRangeTests: XCTestCase {
    private func timeRange(_ endpoint: Endpoint) -> String? {
        endpoint.queryItems.first { $0.name == "time_range" }?.value
    }

    func testPostsOmitsTimeRangeByDefault() {
        XCTAssertFalse(Endpoint.posts().queryItems.contains { $0.name == "time_range" })
    }

    func testPostsSendsSelectedTimeRangeExactly() {
        XCTAssertEqual(timeRange(.posts(timeRange: "1m")), "1m")
        XCTAssertEqual(timeRange(.posts(timeRange: "1w")), "1w")
    }

    func testPostSearchExplicitAllTimePreservesQueryAndCursor() {
        XCTAssertNil(timeRange(.posts(query: "ordinary search")))
        let endpoint = Endpoint.posts(query: "old post", limit: 8, after: "opaque==", timeRange: "all")
        XCTAssertEqual(timeRange(endpoint), "all")
        XCTAssertEqual(endpoint.queryItems.first { $0.name == "q" }?.value, "old post")
        XCTAssertEqual(endpoint.queryItems.first { $0.name == "after" }?.value, "opaque==")
    }
}

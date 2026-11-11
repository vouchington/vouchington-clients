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
}

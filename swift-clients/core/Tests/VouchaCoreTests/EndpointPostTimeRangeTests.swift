import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointPostTimeRangeTests: XCTestCase {
    func testPostsOmitsTimeRangeByDefault() {
        XCTAssertFalse(Endpoint.posts().queryItems.contains { $0.name == "time_range" })
    }

    func testPostsSendsSelectedTimeRangeExactly() {
        XCTAssertTrue(Endpoint.posts(timeRange: "1m").queryItems.contains(URLQueryItem(name: "time_range", value: "1m")))
        XCTAssertTrue(Endpoint.posts(timeRange: "1w").queryItems.contains(URLQueryItem(name: "time_range", value: "1w")))
    }
}

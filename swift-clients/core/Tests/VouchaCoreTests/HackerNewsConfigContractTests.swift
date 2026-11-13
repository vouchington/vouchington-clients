import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class HackerNewsConfigContractTests: XCTestCase {
    func testFixtureDecodesVisibilityFlagAndTimeRangeCap() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            HackerNewsConfigResponse.self, from: ApiFixtureLoader.data("native.hacker-news-config.default")
        )
        XCTAssertTrue(response.referencesVisible)
        XCTAssertEqual(response.postTimeRangeCap, "1y")
    }

    func testEndpointIsAPlainGet() {
        let endpoint = Endpoint.hackerNewsConfig
        XCTAssertEqual(endpoint.method, .GET)
        XCTAssertEqual(endpoint.path, "/api/v1/hacker-news-config")
        XCTAssertTrue(endpoint.queryItems.isEmpty)
    }
}

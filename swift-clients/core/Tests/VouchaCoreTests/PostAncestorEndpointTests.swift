import Foundation
@testable import VouchaAPI
import XCTest

final class PostAncestorEndpointTests: XCTestCase {
    func testPaginationQueryUsesLimitBeforeCursor() {
        let endpoint = Endpoint.postAncestors(postId: "comment 1", after: "cursor 1", limit: 5)

        XCTAssertEqual(endpoint.path, "/api/v1/posts/comment%201/ancestors")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "5"),
            URLQueryItem(name: "after", value: "cursor 1")
        ])
    }
}

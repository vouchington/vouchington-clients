@testable import VouchaAPI
import XCTest

final class UserPostCollectionEndpointTests: XCTestCase {
    func testUserPostCollectionForwardsOpaqueCursorAndLimit() {
        let endpoint = Endpoint.userPosts(
            userId: "user/abc",
            listType: "saved posts",
            after: "opaque+/=cursor",
            limit: 40
        )

        XCTAssertEqual(endpoint.path, "/api/v1/users/user%2Fabc/posts/saved%20posts")
        XCTAssertEqual(endpoint.queryItems, [
            .init(name: "limit", value: "40"),
            .init(name: "after", value: "opaque+/=cursor")
        ])
    }
}

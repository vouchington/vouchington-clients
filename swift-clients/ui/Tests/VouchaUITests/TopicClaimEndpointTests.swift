@testable import VouchaAPI
import XCTest

final class TopicClaimEndpointVisibilityTests: XCTestCase {
    func testTopicClaimEndpointsStayReachableFromTheUIPackage() {
        let mine = Endpoint.myTopicClaims(after: "cursor-1", limit: 1)
        XCTAssertEqual(mine.path, "/api/v1/my/topic-claims")
        XCTAssertEqual(mine.queryItems.map(\.name), ["after", "limit"])

        let topic = Endpoint.topicClaims(
            idOrSlug: "topic-1",
            mineOnly: true,
            activeOnly: true,
            after: "cursor-2",
            limit: 1
        )
        XCTAssertEqual(topic.path, "/api/v1/topics/topic-1/claims")
        XCTAssertEqual(topic.queryItems.map(\.name), ["after", "limit", "mine_only", "active_only"])
    }
}

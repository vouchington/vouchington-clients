@testable import VouchaAPI
import XCTest

final class TopicClaimEndpointTests: XCTestCase {
    func testTopicClaimListsForwardTheOpaqueCursor() {
        let mine = Endpoint.myTopicClaims(after: "cursor-1", limit: 1)
        XCTAssertEqual(mine.path, "/api/v1/my/topic-claims")
        XCTAssertEqual(query(mine), ["after": "cursor-1", "limit": "1"])

        let topic = Endpoint.topicClaims(
            idOrSlug: "topic-1",
            mineOnly: true,
            activeOnly: false,
            after: "cursor-2",
            limit: 2
        )
        XCTAssertEqual(topic.path, "/api/v1/topics/topic-1/claims")
        XCTAssertEqual(
            query(topic),
            ["after": "cursor-2", "limit": "2", "mine_only": "true", "active_only": "false"]
        )
    }

    private func query(_ endpoint: Endpoint) -> [String: String] {
        Dictionary(uniqueKeysWithValues: endpoint.queryItems.map { item in
            (item.name, item.value ?? "")
        })
    }
}

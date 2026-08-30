import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class EndpointTopHashtagsTests: XCTestCase {
    func testTopicAliasLinkEndpointUsesExpectedRoute() {
        assertEndpoint(
            Endpoint.linkTopicAlias(topicId: "topic 1", aliasId: "alias 1"),
            method: .POST,
            path: "/api/v1/topics/topic%201/aliases/alias%201",
            body: [:]
        )
    }

    func testTopHashtagsResponseDecodesTopicSidecarsAndPagination() throws {
        let json = Data(
            #"""
            {"results":[{"topic_alias_id":"alias-1","hashtag":"swift-ui","item_count":12,
            "contributor_count":4,"latest_content_id":"content-1","topic_id":"topic-1"}],
            "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null},
            "topics":{"topic-1":{"id":"topic-1","name":"SwiftUI","slug":"swift-ui",
            "markdown":null,"topic_type":"topic","hostname_id":null,"hostname":null,
            "logo_image_id":null,"hero_image_id":null,"homepage_url_id":null,
            "lingua_rs_detected_language":null,"referral_program_id":null,
            "referral_program_slug":null,"rewards_program_id":null,
            "created_at":"2026-01-01T00:00:00.000Z"}}}
            """#.utf8
        )

        let decoded = try makeVouchaDecoder().decode(TopHashtagsResponse.self, from: json)

        XCTAssertEqual(decoded.results.first?.id, "alias-1")
        XCTAssertEqual(decoded.results.first?.hashtag, "swift-ui")
        XCTAssertEqual(decoded.results.first?.contributorCount, 4)
        XCTAssertEqual(decoded.topics["topic-1"]?.name, "SwiftUI")
        XCTAssertTrue(decoded.pageInfo.hasNextPage)
    }

    func testTopHashtagsEndpointIncludesFiltersAndCursor() {
        let endpoint = Endpoint.topHashtags(
            query: "swift_dev",
            mapping: .unlinked,
            after: "cursor-4",
            limit: 6
        )

        XCTAssertEqual(endpoint.path, "/api/v1/topic-recommendations/top-hashtags")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "6"),
            URLQueryItem(name: "mapping", value: "unlinked"),
            URLQueryItem(name: "q", value: "swift_dev"),
            URLQueryItem(name: "after", value: "cursor-4")
        ])
    }
}

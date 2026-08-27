import Foundation
@testable import VouchaModels
import XCTest

final class SearchAndSourceDecodingTests: XCTestCase {
    func testDecodesOmnisearchResponse() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase

        let json = Data(
            """
            {
              "topics": [{"id": "topic-1", "name": "Swift", "slug": "swift", "topic_type": "topic"}],
              "posts": [{"id": "post-1", "post_type": "discussion", "title": "Swift 6"}],
              "news": [{"id": "news-1", "url": "https://example.com/news/swift", "title": "Swift news", "feed_title": "Example Feed"}],
              "domains": [{"id": "domain-1", "hostname": "example.com"}],
              "communities": [{"id": "community-1", "name": "Swift Forums", "slug": "swift-forums", "bookmarked": true}]
            }
            """.utf8
        )

        let response = try decoder.decode(OmnisearchResponse.self, from: json)
        XCTAssertEqual(response.topics.first?.topicType, "topic")
        XCTAssertEqual(response.posts.first?.postType, "discussion")
        XCTAssertEqual(response.news.first?.feedTitle, "Example Feed")
        XCTAssertEqual(response.domains.first?.hostname, "example.com")
        XCTAssertEqual(response.communities.first?.bookmarked, true)
    }

    func testDecodesRssFeedSourceListResponse() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase

        let json = Data(
            """
            {
              "results": [{
                "id": "src-1",
                "title": "The Verge",
                "is_enabled": true,
                "is_discoverable": true,
                "feed_type": "article",
                "rss_feed_url": { "url": "https://www.theverge.com/rss/index.xml", "canonical_url_id": null },
                "etag": null,
                "home_page_url": null,
                "last_modified_at": null,
                "hostname": { "hostname": "theverge.com", "id": "host-1" },
                "publisher_type": null,
                "podcast_show": null
              }],
              "page_info": { "has_next_page": true, "end_cursor": "cursor-2", "start_cursor": "cursor-1" },
              "topic_elections": { "topic-1": { "id": "topic-1", "votes_score_net": 3.25, "votes_count_up": 4, "votes_count_down": 1 } },
              "hostname_elections": { "host-1": { "id": "host-1", "votes_score_net": 2.5, "votes_count_up": 3, "votes_count_down": 1 } },
              "bookmarks": { "src-1": { "follow": true } }
            }
            """.utf8
        )

        let response = try decoder.decode(RssFeedSourceListResponse.self, from: json)
        XCTAssertEqual(response.results.first?.id, "src-1")
        XCTAssertEqual(response.results.first?.displayHost, "theverge.com")
        XCTAssertTrue(response.pageInfo.hasNextPage)
        XCTAssertEqual(response.pageInfo.endCursor, "cursor-2")
        XCTAssertEqual(response.topicElections["topic-1"]?.votesScoreNet, 3.25)
        XCTAssertEqual(response.hostnameElections["host-1"]?.votesCountUp, 3)
        XCTAssertEqual(response.bookmarks?["src-1"]?["follow"], true)
    }

    func testDecodesRssFeedSourceListElectionVotes() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601

        let json = Data(
            """
            {
              "results": [],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null },
              "topic_elections": {},
              "hostname_elections": {},
              "election_votes": {
                "topic-1": {
                  "__entity_type": "election_vote",
                  "user_id": "user-1",
                  "entity_id": "topic-1",
                  "choice": "dislike",
                  "created_at": "2026-01-01T00:00:00Z"
                }
              }
            }
            """.utf8
        )

        let response = try decoder.decode(RssFeedSourceListResponse.self, from: json)
        XCTAssertEqual(response.electionVotes?["topic-1"]?.choice, .dislike)
        XCTAssertEqual(response.electionVotes?["topic-1"]?.entityId, "topic-1")
    }
}

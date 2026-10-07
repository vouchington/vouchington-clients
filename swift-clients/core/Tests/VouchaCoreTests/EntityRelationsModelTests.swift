import Foundation
@testable import VouchaModels
import XCTest

final class EntityRelationsModelTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }()

    func testEntityRelationObjectDataUsesExpectedDisplayFallbacks() throws {
        let data = try decoder.decode(
            EntityRelationObjectData.self,
            from: Data(
                """
                {
                  "id": "topic-1",
                  "name": "Swift",
                  "slug": "swift",
                  "title": null,
                  "url": null,
                  "pathname": null,
                  "username": null,
                  "post_type": null,
                  "topic_type": "topic",
                  "feed_type": null,
                  "hostname": { "id": "host-1", "hostname": "example.com" },
                  "latest_crawl": { "title": "Example Crawl", "image_url": "https://example.com/crawl.png" }
                }
                """.utf8
            )
        )

        XCTAssertEqual(data.displayTitle, "Swift")
        XCTAssertEqual(data.displayDetail, "topic")
    }

    func testEntityRelationObjectDataFallsBackToHostnameUrlAndId() throws {
        let data = try decoder.decode(
            EntityRelationObjectData.self,
            from: Data(
                """
                {
                  "id": "url-1",
                  "name": null,
                  "slug": null,
                  "title": null,
                  "url": "https://example.com/post",
                  "pathname": "/post",
                  "username": null,
                  "post_type": null,
                  "topic_type": null,
                  "feed_type": null,
                  "hostname": null,
                  "latest_crawl": null
                }
                """.utf8
            )
        )

        XCTAssertEqual(data.displayTitle, "https://example.com/post")
        XCTAssertEqual(data.displayDetail, "/post")
    }

    func testEntityRelationDecodesAMaskedCreator() throws {
        let relation = try decoder.decode(
            EntityRelation.self,
            from: Data(
                """
                {
                  "id": "relation-1",
                  "subject_id": "post-1",
                  "object_id": "post-2",
                  "created_at": "2026-01-01T00:00:00Z",
                  "created_by_id": null,
                  "object_data": {
                    "id": "post-2",
                    "title": "Related Post",
                    "post_type": "discussion",
                    "declared_language": "fr",
                    "lingua_rs_detected_language": "en"
                  }
                }
                """.utf8
            )
        )

        XCTAssertNil(relation.createdById)
        XCTAssertEqual(relation.objectData.displayTitle, "Related Post")
        XCTAssertEqual(relation.objectData.declaredLanguage, "fr")
        XCTAssertEqual(relation.objectData.linguaRsDetectedLanguage, "en")
    }

    func testEntityRelationsPageAndPublisherTypesDecode() throws {
        let response = try decoder.decode(
            EntityRelationsResponse.self,
            from: Data(
                """
                {
                  "results": [{ "id": "relation-1" }],
                  "page_info": { "has_next_page": true, "end_cursor": "cursor-1", "start_cursor": null },
                  "entity_relations": {
                    "relation-1": {
                      "id": "relation-1",
                      "subject_id": "post-1",
                      "object_id": "topic-1",
                      "created_at": "2026-01-01T00:00:00Z",
                      "created_by_id": "user-1",
                      "deleted_at": null,
                      "deleted_by_id": null,
                      "order_index": 2,
                      "votes_count_up": 3,
                      "votes_count_down": 1,
                      "votes_score_net": 2,
                      "votes_score_sort": 2,
                      "object_data": {
                        "id": "topic-1",
                        "name": "Swift",
                        "slug": "swift",
                        "topic_type": "topic"
                      }
                    }
                  },
                  "election_votes": {
                    "relation-1": {
                      "__entity_type": "election_vote",
                      "entity_id": "relation-1",
                      "user_id": "user-1",
                      "choice": "dispute",
                      "created_at": "2026-01-01T00:00:00Z"
                    }
                  }
                }
                """.utf8
            )
        )

        XCTAssertEqual(response.pageInfo.hasNextPage, true)
        XCTAssertEqual(response.pageInfo.endCursor, "cursor-1")
        XCTAssertEqual(response.entityRelations["relation-1"]?.createdById, "user-1")
        XCTAssertEqual(response.electionVotes?["relation-1"]?.choice, .dispute)

        let publisherTypes = try decoder.decode(
            PublisherTypesResponse.self,
            from: Data(#"{"publisher_types":[{"id":"publisher-blog","slug":"blog","label":"Blog"}]}"#.utf8)
        )
        XCTAssertEqual(publisherTypes.publisherTypes.first?.label, "Blog")
    }
}

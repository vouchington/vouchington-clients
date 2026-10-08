import Foundation
@testable import VouchaModels
import XCTest

final class RssFeedItemDecodingTests: XCTestCase {
    func testEmbeddedFeedPreservesPublicProvenanceFromDirectFeed() throws {
        let provenanceCases = [
            (
                #"""
                {
                  "via": "mcp",
                  "app": {
                    "kind": "verified",
                    "client_id": "voucha_fixture_agent",
                    "client_name": "Fixture Agent"
                  }
                }
                """#,
                "mcp"
            ),
            (
                #"{"via":"api","app":null}"#,
                "api"
            )
        ]

        for (index, (provenance, via)) in provenanceCases.enumerated() {
            let sourceJSON = """
            {
              "id": "feed-\(index)",
              "title": "Example Feed",
              "feed_type": "article",
              "rss_feed_url": { "url": "https://example.com/feed.xml" },
              "etag": null,
              "home_page_url": null,
              "last_modified_at": null,
              "hostname": null,
              "publisher_type": null,
              "podcast_show": null,
              "provenance": \(provenance)
            }
            """
            let source = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(sourceJSON.utf8)) as? [String: Any])
            let directFeed = try makeVouchaDecoder().decode(RssFeedSource.self, from: Data(sourceJSON.utf8))
            let itemData = try JSONSerialization.data(withJSONObject: [
                "id": "item-\(index)",
                "rss_feed": source
            ])
            let item = try makeVouchaDecoder().decode(RssFeedItem.self, from: itemData)
            XCTAssertEqual(directFeed.provenance?.via, via)
            XCTAssertEqual(item.rssFeed?.id, directFeed.id)
            XCTAssertEqual(item.rssFeed?.provenance, directFeed.provenance)
        }
    }

    func testFallsBackToNestedRssFeedIdWhenFlatFieldIsMissing() throws {
        let decoder = makeVouchaDecoder()
        let json = Data(
            #"""
            {
              "id": "item-1",
              "rss_feed": {
                "id": "feed-1",
                "title": "Example Podcast",
                "feed_type": "podcast",
                "rss_feed_url": {
                  "url": "https://example.com/rss",
                  "canonical_url_id": null
                },
                "etag": null,
                "home_page_url": null,
                "last_modified_at": null,
                "hostname": null,
                "topic": null,
                "publisher_type": null,
                "podcast_show": null
              },
              "data": {
                "title": "Episode title",
                "link": "https://example.com/episode",
                "media_type": "audio",
                "enclosure_url": "https://example.com/episode.mp3",
                "enclosure_type": "audio/mpeg",
                "duration_seconds": 1800,
                "thumbnail_url": "https://example.com/episode.jpg",
                "video_id": null,
                "video_platform": null
              },
              "media_content": {
                "url": "https://example.com/episode.mp3",
                "type": "audio/mpeg",
                "medium": "audio",
                "duration": 1800
              }
            }
            """#
            .utf8
        )

        let item = try decoder.decode(RssFeedItem.self, from: json)
        XCTAssertEqual(item.rssFeedId, "feed-1")
        XCTAssertEqual(item.rssFeed?.id, "feed-1")
        XCTAssertEqual(item.title, "Episode title")
        XCTAssertEqual(item.link, "https://example.com/episode")
        XCTAssertEqual(item.mediaType, "audio")
        XCTAssertEqual(item.durationSeconds, 1_800)
    }

    func testDecodesPartialEmbeddedRssFeedSidecarWithoutUrlOrTitle() throws {
        let decoder = makeVouchaDecoder()
        let json = Data(
            #"""
            {
              "id": "item-2",
              "rss_feed": {
                "id": "feed-2",
                "title": null,
                "hostname": null,
                "topic": null,
                "publisher_type": null,
                "podcast_show": null
              },
              "data": {
                "title": "Episode title",
                "link": "https://example.com/episode-2",
                "media_type": "audio",
                "enclosure_url": "https://example.com/episode-2.mp3",
                "enclosure_type": "audio/mpeg",
                "duration_seconds": 900,
                "thumbnail_url": null,
                "video_id": null,
                "video_platform": null
              },
              "media_content": {
                "url": "https://example.com/episode-2.mp3",
                "type": "audio/mpeg",
                "medium": "audio",
                "duration": 900
              }
            }
            """#
            .utf8
        )

        let item = try decoder.decode(RssFeedItem.self, from: json)
        XCTAssertEqual(item.rssFeedId, "feed-2")
        XCTAssertEqual(item.title, "Episode title")
        XCTAssertEqual(item.link, "https://example.com/episode-2")
        XCTAssertEqual(item.durationSeconds, 900)
    }

    func testDecodesCategoryObjectsFromFixtureShape() throws {
        let decoder = makeVouchaDecoder()
        let json = Data(
            #"""
            {
              "id": "item-1",
              "rss_feed_id": "feed-1",
              "title": "Test Article",
              "description": "A short snippet",
              "content": null,
              "link": "https://example.com/article",
              "published_at": "2026-01-01T00:00:00Z",
              "creator": null,
              "categories": [
                {
                  "id": "cat-1",
                  "category_text": "technology",
                  "topic": {
                    "id": "topic-1",
                    "name": "Technology",
                    "slug": "technology",
                    "topic_type": "topic"
                  },
                  "votes_score_net": 3.25,
                  "hashtag": {
                    "id": "hashtag-1",
                    "key": "technology",
                    "display_token": "#Technology",
                    "topic_id": "topic-1"
                  }
                },
                {
                  "id": null,
                  "category_text": "news",
                  "topic": null,
                  "votes_score_net": null,
                  "hashtag": null
                }
              ],
              "media_content": null
            }
            """#
            .utf8
        )

        let item = try decoder.decode(RssFeedItem.self, from: json)
        XCTAssertEqual(item.id, "item-1")
        XCTAssertEqual(item.categories?.count, 2)
        XCTAssertEqual(item.categories?.first?.categoryText, "technology")
        XCTAssertEqual(item.categories?.first?.topic?.id, "topic-1")
        XCTAssertEqual(item.categories?.first?.votesScoreNet, 3.25)
        XCTAssertEqual(item.categories?.first?.hashtag?.id, "hashtag-1")
        XCTAssertEqual(item.categories?.first?.hashtag?.key, "technology")
        XCTAssertEqual(item.categories?.first?.hashtag?.displayToken, "#Technology")
        XCTAssertEqual(item.categories?.first?.hashtag?.topicId, "topic-1")
        XCTAssertNil(item.categories?.last?.id)
        XCTAssertNil(item.categories?.last?.topic)
        XCTAssertNil(item.categories?.last?.votesScoreNet)
        XCTAssertNil(item.categories?.last?.hashtag)
    }
}

final class TopicDecodingTests: XCTestCase {
    func testDecodesTopicElectionAndViewerVote() throws {
        let decoder = makeVouchaDecoder()
        let json = Data(
            #"""
            {
              "id": "topic-1",
              "name": "Swift",
              "slug": "swift",
              "markdown": "Native apps",
              "topic_type": "topic",
              "hostname_id": null,
              "hostname": null,
              "logo_image_id": "logo-1",
              "hero_image_id": null,
              "homepage_url_id": null,
              "lingua_rs_detected_language": null,
              "referral_program_id": null,
              "referral_program_slug": null,
              "rewards_program_id": null,
              "created_at": "2026-01-01T00:00:00Z",
              "election": {
                "votes_score_net": 5,
                "votes_count_up": 6,
                "votes_count_down": 1,
                "my_vote": "like"
              }
            }
            """#
            .utf8
        )

        let topic = try decoder.decode(Topic.self, from: json)
        XCTAssertEqual(topic.id, "topic-1")
        XCTAssertEqual(topic.name, "Swift")
        XCTAssertEqual(topic.logoImageId, "logo-1")
        XCTAssertEqual(topic.election?.votesScoreNet, 5)
        XCTAssertEqual(topic.election?.votesCountUp, 6)
        XCTAssertEqual(topic.election?.votesCountDown, 1)
        XCTAssertEqual(topic.election?.myVote, .like)
    }

    func testTopicInitializerDefaultsElectionToNil() {
        let topic = Topic(
            id: "topic-2",
            name: "News",
            slug: "news",
            markdown: nil,
            topicType: "topic",
            hostnameId: nil,
            logoImageId: nil,
            createdAt: Date(timeIntervalSince1970: 0)
        )

        XCTAssertEqual(topic.slug, "news")
        XCTAssertNil(topic.election)
    }

    func testTopicInitializerPreservesElection() {
        let election = TopicElection(
            votesScoreNet: 9,
            votesCountUp: 12,
            votesCountDown: 3,
            myVote: .dislike
        )
        let topic = Topic(
            id: "topic-3",
            name: "Video",
            slug: "video",
            markdown: "Native video",
            topicType: "topic",
            hostnameId: "host-1",
            logoImageId: "logo-1",
            createdAt: Date(timeIntervalSince1970: 0),
            election: election
        )

        XCTAssertEqual(topic.election?.votesScoreNet, 9)
        XCTAssertEqual(topic.election?.votesCountUp, 12)
        XCTAssertEqual(topic.election?.votesCountDown, 3)
        XCTAssertEqual(topic.election?.myVote, .dislike)
    }
}

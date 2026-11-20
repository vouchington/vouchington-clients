import Foundation
@testable import VouchaModels
import XCTest

// MARK: - Page<T> decoding

final class PageDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        return d
    }()

    func testDecodesDataAndMeta() throws {
        let json = Data("""
        {"results":[{"id":"abc","username":"alice","roles":[]}],"page_info":{"has_next_page":true,"end_cursor":"cursor123","start_cursor":null}}
        """.utf8)
        let page = try decoder.decode(Page<PublicUser>.self, from: json)
        XCTAssertEqual(page.results[0].id, "abc")
        XCTAssertEqual(page.pageInfo.endCursor, "cursor123")
        XCTAssertTrue(page.pageInfo.hasNextPage)
    }

    func testDecodesEmptyPage() throws {
        let json = Data(
            #"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8
        )
        let page = try decoder.decode(Page<PublicUser>.self, from: json)
        XCTAssertTrue(page.results.isEmpty)
        XCTAssertNil(page.pageInfo.endCursor)
    }
}

// MARK: - RssFeedSource decoding

final class RssFeedSourceDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        return d
    }()

    /// Fixture derived from a real `/api/v1/rss-feeds` response (article source).
    func testDecodesArticleSource() throws {
        let json = Data("""
        {
          "id": "src-001",
          "title": "The Verge",
          "is_enabled": true,
          "is_discoverable": true,
          "etag": null,
          "last_modified_at": null,
          "last_fetched_at": null,
          "feed_type": "article",
          "rss_feed_url": { "url": "https://www.theverge.com/rss/index.xml", "canonical_url_id": null },
          "home_page_url": { "url": "https://www.theverge.com", "canonical_url_id": null },
          "hostname": { "hostname": "theverge.com", "id": "host-1" },
          "topic": { "id": "t1", "slug": "tech", "topic_type": "topic", "name": "Tech", "hostname_id": null, "hostname": null, "logo_image_id": null, "hero_image_id": null, "homepage_url_id": null, "lingua_rs_detected_language": null, "referral_program_id": null, "referral_program_slug": null, "rewards_program_id": null },
          "publisher_type": {
            "id": "publisher-type-blog",
            "name": "Blog",
            "slug": "blog",
            "topic_type": "publisher_type"
          },
          "podcast_show": null
        }
        """.utf8)

        let source = try decoder.decode(RssFeedSource.self, from: json)
        XCTAssertEqual(source.id, "src-001")
        XCTAssertEqual(source.title, "The Verge")
        XCTAssertEqual(source.feedType, "article")
        XCTAssertEqual(source.rssFeedUrl.url, "https://www.theverge.com/rss/index.xml")
        XCTAssertEqual(source.hostname?.hostname, "theverge.com")
        XCTAssertEqual(source.publisherType?.id, "publisher-type-blog")
        XCTAssertEqual(source.publisherType?.topicType, "publisher_type")
        XCTAssertNil(source.podcastShow)
        XCTAssertEqual(source.displayHost, "theverge.com")
    }

    /// Podcast source — includes podcastShow with coverArtUrl.
    func testDecodesPodcastSource() throws {
        let json = Data("""
        {
          "id": "src-002",
          "title": "99% Invisible",
          "is_enabled": true,
          "is_discoverable": true,
          "etag": null,
          "last_modified_at": null,
          "last_fetched_at": null,
          "feed_type": "podcast",
          "rss_feed_url": { "url": "https://feeds.99percentinvisible.org/99percentinvisible", "canonical_url_id": null },
          "home_page_url": null,
          "hostname": null,
          "topic": { "id": "t2", "slug": "design", "topic_type": "topic", "name": "Design", "hostname_id": null, "hostname": null, "logo_image_id": null, "hero_image_id": null, "homepage_url_id": null, "lingua_rs_detected_language": null, "referral_program_id": null, "referral_program_slug": null, "rewards_program_id": null },
          "publisher_type": null,
          "podcast_show": {
            "itunes_author": "Roman Mars",
            "itunes_owner_name": null,
            "cover_art_url": "https://example.com/cover.jpg",
            "is_explicit": false,
            "itunes_type": "episodic"
          }
        }
        """.utf8)

        let source = try decoder.decode(RssFeedSource.self, from: json)
        XCTAssertEqual(source.id, "src-002")
        XCTAssertEqual(source.feedType, "podcast")
        XCTAssertEqual(source.podcastShow?.coverArtUrl, "https://example.com/cover.jpg")
        XCTAssertNil(source.hostname)
        // displayHost falls back to URL host
        XCTAssertEqual(source.displayHost, "feeds.99percentinvisible.org")
    }

    /// Podcast source with null podcast_show — must not throw.
    func testDecodesSourceWithNullPodcastShow() throws {
        let json = Data("""
        {
          "id": "src-003",
          "title": "Some Podcast",
          "is_enabled": true,
          "is_discoverable": true,
          "etag": null,
          "last_modified_at": null,
          "last_fetched_at": null,
          "feed_type": "podcast",
          "rss_feed_url": { "url": "https://example.com/feed.xml", "canonical_url_id": null },
          "home_page_url": null,
          "hostname": null,
          "topic": { "id": "t3", "slug": "misc", "topic_type": "topic", "name": "Misc", "hostname_id": null, "hostname": null, "logo_image_id": null, "hero_image_id": null, "homepage_url_id": null, "lingua_rs_detected_language": null, "referral_program_id": null, "referral_program_slug": null, "rewards_program_id": null },
          "publisher_type": null,
          "podcast_show": null
        }
        """.utf8)

        let source = try decoder.decode(RssFeedSource.self, from: json)
        XCTAssertNil(source.podcastShow)
    }

    func testDecodesSourcesList() throws {
        let json = Data("""
        {
          "results": [
            {
              "id": "src-004",
              "title": "Hacker News",
              "is_enabled": true,
              "is_discoverable": true,
              "etag": null,
              "last_modified_at": null,
              "last_fetched_at": null,
              "feed_type": "article",
              "rss_feed_url": { "url": "https://news.ycombinator.com/rss", "canonical_url_id": null },
              "home_page_url": null,
              "etag": null,
              "last_modified_at": null,
              "home_page_url": null,
              "hostname": { "hostname": "news.ycombinator.com", "id": "hn" },
              "topic": { "id": "t4", "slug": "tech", "topic_type": "topic", "name": "Tech", "hostname_id": null, "hostname": null, "logo_image_id": null, "hero_image_id": null, "homepage_url_id": null, "lingua_rs_detected_language": null, "referral_program_id": null, "referral_program_slug": null, "rewards_program_id": null },
              "publisher_type": null,
              "podcast_show": null
            }
          ]
        }
        """.utf8)

        struct Response: Decodable { let results: [RssFeedSource] }
        let response = try decoder.decode(Response.self, from: json)
        XCTAssertEqual(response.results.count, 1)
        XCTAssertEqual(response.results[0].id, "src-004")
    }
}

// MARK: - Profile model decoding

final class ProfileDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        return d
    }()

    func testDecodesProfile() throws {
        struct ProfileEnvelope: Decodable {
            let profile: Profile
        }

        let envelope = try decoder.decode(
            ProfileEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.profile.default")
        )
        let profile = envelope.profile
        XCTAssertEqual(profile.id, "user-abc")
        XCTAssertEqual(profile.markdown, "Hello world")
    }

    func testDecodesEmptyMarkdown() throws {
        let json = Data("""
        {"id":"p2","markdown":""}
        """.utf8)
        let profile = try decoder.decode(Profile.self, from: json)
        XCTAssertEqual(profile.id, "p2")
        XCTAssertTrue(profile.markdown.isEmpty)
    }

    func testProfileIdIsIdentifiable() throws {
        let json = Data("""
        {"id":"profile-xyz","markdown":"bio text"}
        """.utf8)
        let profile = try decoder.decode(Profile.self, from: json)
        XCTAssertEqual(profile.id, "profile-xyz")
    }
}

// MARK: - Entity relations and publisher types decoding

final class EntityRelationsDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        d.dateDecodingStrategy = .iso8601
        return d
    }()

    func testDecodesEntityRelationsResponse() throws {
        let json = Data("""
        {
          "results": [{ "id": "relation-1" }],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null },
          "entity_relations": {
            "relation-1": {
              "id": "relation-1",
              "subject_id": "post-1",
              "object_id": "topic-1",
              "created_at": "2026-01-01T00:00:00Z",
              "created_by_id": "user-1",
              "deleted_at": null,
              "deleted_by_id": null,
              "order_index": 1,
              "votes_count_up": 2,
              "votes_count_down": 1,
              "votes_score_net": 1,
              "votes_score_sort": 1,
              "object_data": {
                "id": "topic-1",
                "name": "Swift",
                "slug": "swift",
                "topic_type": "topic"
              }
            }
          },
          "entity_relation_elections": {
            "relation-1": {
              "id": "relation-1",
              "votes_score_net": 1,
              "votes_count_up": 2,
              "votes_count_down": 1
            }
          },
          "election_votes": {
            "relation-1": {
              "__entity_type": "election_vote",
              "entity_id": "relation-1",
              "user_id": "user-1",
              "choice": "confirm",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """.utf8)

        let response = try decoder.decode(EntityRelationsResponse.self, from: json)
        XCTAssertEqual(response.results.first?.id, "relation-1")
        XCTAssertEqual(response.entityRelations["relation-1"]?.objectData.displayTitle, "Swift")
        XCTAssertEqual(response.entityRelationElections?["relation-1"]?.votesCountUp, 2)
        XCTAssertEqual(response.electionVotes?["relation-1"]?.choice, .confirm)
    }

    func testDecodesPublisherTypesResponse() throws {
        let json = Data("""
        {
          "publisher_types": [
            { "id": "publisher-blog", "slug": "blog", "label": "Blog" }
          ]
        }
        """.utf8)

        let response = try decoder.decode(PublisherTypesResponse.self, from: json)
        XCTAssertEqual(response.publisherTypes.first?.slug, "blog")
        XCTAssertEqual(response.publisherTypes.first?.label, "Blog")
    }
}

// MARK: - PodcastPlaybackPosition decoding

final class PodcastPlaybackPositionTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        return d
    }()

    func testDecodesResponseWithPosition() throws {
        let json = Data("""
        {"playback_position":{"position_seconds":45.5,"completed_at":null}}
        """.utf8)
        let response = try decoder.decode(PodcastPlaybackPositionResponse.self, from: json)
        let pos = try XCTUnwrap(response.playbackPosition)
        XCTAssertEqual(pos.positionSeconds, 45.5, accuracy: 0.001)
        XCTAssertNil(pos.completedAt)
        XCTAssertFalse(pos.isCompleted)
    }

    func testDecodesResponseWithNilPosition() throws {
        let json = Data("""
        {"playback_position":null}
        """.utf8)
        let response = try decoder.decode(PodcastPlaybackPositionResponse.self, from: json)
        XCTAssertNil(response.playbackPosition)
    }

    func testIsCompletedTrueWhenCompletedAtSet() throws {
        let json = Data("""
        {"playback_position":{"position_seconds":1800.0,"completed_at":"2026-01-15T10:00:00Z"}}
        """.utf8)
        let response = try decoder.decode(PodcastPlaybackPositionResponse.self, from: json)
        let pos = try XCTUnwrap(response.playbackPosition)
        XCTAssertTrue(pos.isCompleted)
    }
}

// MARK: - PodcastChapter decoding

final class PodcastChapterTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        return d
    }()

    func testDecodesChapterResponse() throws {
        let json = Data("""
        {
          "chapters": [
            {
              "start_seconds": 0,
              "end_seconds": 42.5,
              "title": "Intro",
              "url": "https://example.com/intro",
              "image_url": "https://images.example.com/sideload/intro.jpg",
              "is_visible": true
            }
          ]
        }
        """.utf8)

        let response = try decoder.decode(PodcastChapterResponse.self, from: json)
        let chapter = try XCTUnwrap(response.chapters.first)
        XCTAssertEqual(chapter.startSeconds, 0, accuracy: 0.001)
        XCTAssertEqual(try XCTUnwrap(chapter.endSeconds), 42.5, accuracy: 0.001)
        XCTAssertEqual(chapter.title, "Intro")
        XCTAssertEqual(chapter.url, "https://example.com/intro")
        XCTAssertEqual(chapter.imageURL, "https://images.example.com/sideload/intro.jpg")
        XCTAssertTrue(chapter.isVisible)
    }

    func testDecodesHiddenChapterFlag() throws {
        let json = Data("""
        {
          "chapters": [
            {
              "start_seconds": 120,
              "end_seconds": null,
              "title": "Bonus",
              "url": null,
              "image_url": null,
              "is_visible": false
            }
          ]
        }
        """.utf8)

        let response = try decoder.decode(PodcastChapterResponse.self, from: json)
        let chapter = try XCTUnwrap(response.chapters.first)
        XCTAssertFalse(chapter.isVisible)
        XCTAssertNil(chapter.endSeconds)
        XCTAssertNil(chapter.url)
        XCTAssertNil(chapter.imageURL)
    }
}

// MARK: - Vote sidecar decoding

final class VoteSidecarDecodingTests: XCTestCase {
    private let decoder = makeVouchaDecoder()

    func testDecodesTopicElectionAndMyVote() throws {
        let data = Data("""
        {"votes_score_net":5.5,"votes_count_up":8,"votes_count_down":3,"my_vote":"dislike"}
        """.utf8)
        let election = try decoder.decode(TopicElection.self, from: data)
        XCTAssertEqual(election.votesScoreNet, 5.5)
        XCTAssertEqual(election.votesCountUp, 8)
        XCTAssertEqual(election.votesCountDown, 3)
        XCTAssertEqual(election.myVote, .dislike)
    }

    func testDecodesRssFeedItemElectionAndMyVote() throws {
        let data = Data("""
        {"votes_score_net":12.25,"votes_count_up":14,"votes_count_down":2,"my_vote":"like"}
        """.utf8)
        let election = try decoder.decode(RssFeedItemElection.self, from: data)
        XCTAssertEqual(election.votesScoreNet, 12.25)
        XCTAssertEqual(election.votesCountUp, 14)
        XCTAssertEqual(election.votesCountDown, 2)
        XCTAssertEqual(election.myVote, .like)
    }
}

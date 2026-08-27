import Foundation
import ViewInspector
@testable import VouchaDesignSystem
import VouchaModels
import XCTest

@MainActor
final class RssFeedSourceCardTests: XCTestCase {
    private func makeSource(id: String = "src-1", feedType: String = "article") throws -> RssFeedSource {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let json = Data("""
        {
          "id":"\(id)",
          "title":"Source \(id)",
          "is_enabled":true,
          "is_discoverable":true,
          "etag":null,
          "last_modified_at":null,
          "last_fetched_at":null,
          "feed_type":"\(feedType)",
          "rss_feed_url":{"url":"https://example.com/\(id).xml","canonical_url_id":null},
          "home_page_url":null,
          "hostname":{"hostname":"example.com","id":"host-1"},
          "topic":{"id":"topic-1","slug":"tech","topic_type":"topic","name":"Tech","hostname_id":null,"hostname":null,"logo_image_id":null,"hero_image_id":null,"homepage_url_id":null,"lingua_rs_detected_language":null,"referral_program_id":null,"referral_program_slug":null,"rewards_program_id":null},
          "publisher_type":null,
          "podcast_show":null
        }
        """.utf8)
        return try decoder.decode(RssFeedSource.self, from: json)
    }

    func testRssFeedSourceCardShowsFollowButtonWhenNotFollowing() throws {
        let sut = try RssFeedSourceCard(source: makeSource(), followAction: {})

        XCTAssertNoThrow(try sut.inspect().find(button: "Follow"))
    }

    func testRssFeedSourceCardShowsUnfollowButtonWhenFollowing() throws {
        let sut = try RssFeedSourceCard(source: makeSource(), isFollowing: true, followAction: {})

        XCTAssertNoThrow(try sut.inspect().find(button: "Following"))
    }

    func testRssFeedSourceCardShowsMuteButtonWhenMuted() throws {
        let sut = try RssFeedSourceCard(source: makeSource(), muteAction: {})

        XCTAssertNoThrow(try sut.inspect().find(button: "Mute"))
    }

    func testRssFeedSourceCardTapsFollowAction() throws {
        var tapped = false
        let sut = try RssFeedSourceCard(source: makeSource(), followAction: { tapped = true })

        try sut.inspect().find(button: "Follow").tap()
        XCTAssertTrue(tapped)
    }

    func testRssFeedSourceCardOmitsActionWhenFollowActionIsNil() throws {
        let sut = try RssFeedSourceCard(source: makeSource())

        XCTAssertThrowsError(try sut.inspect().find(button: "Follow"))
    }

    func testRssFeedSourceCardShowsTopicVoteControlsWhenElectionExists() throws {
        let sut = try RssFeedSourceCard(
            source: makeSource(),
            followAction: {},
            muteAction: {},
            followTopicAction: {},
            muteTopicAction: {},
            topicElection: TopicElection(votesScoreNet: 4.5, votesCountUp: 5, votesCountDown: 1),
            onTopicVote: { _ in }
        )

        XCTAssertEqual(try sut.inspect().find(text: "5").string(), "5")
        XCTAssertEqual(try sut.inspect().find(text: "1").string(), "1")
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Button.self).count, 8)
        XCTAssertNoThrow(try sut.inspect().find(button: "Mute topic"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Vouch"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Neutral"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Clear"))
    }
}

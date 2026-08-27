import Foundation
@testable import VouchaModels
import XCTest

final class ElectionDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }()

    func testDecodesElectionVote() throws {
        let json = Data(
            """
            {
              "__entity_type": "election_vote",
              "user_id": "user-1",
              "entity_id": "topic-1",
              "choice": "vouch",
              "created_at": "2026-01-01T00:00:00Z"
            }
            """.utf8
        )

        let vote = try decoder.decode(ElectionVote.self, from: json)
        XCTAssertEqual(vote.userId, "user-1")
        XCTAssertEqual(vote.entityId, "topic-1")
        XCTAssertEqual(vote.choice, .vouch)
    }

    func testDecodesTopicElectionWithOptionalMyVote() throws {
        let json = Data(
            """
            {
              "votes_score_net": 3.25,
              "votes_count_up": 4,
              "votes_count_down": 1
            }
            """.utf8
        )

        let election = try decoder.decode(TopicElection.self, from: json)
        XCTAssertEqual(election.votesScoreNet, 3.25)
        XCTAssertNil(election.myVote)
    }

    func testDecodesRssFeedItemElectionWithMyVote() throws {
        let json = Data(
            """
            {
              "votes_score_net": 7.5,
              "votes_count_up": 8,
              "votes_count_down": 1,
              "my_vote": "dislike"
            }
            """.utf8
        )

        let election = try decoder.decode(RssFeedItemElection.self, from: json)
        XCTAssertEqual(election.votesScoreNet, 7.5)
        XCTAssertEqual(election.myVote, .dislike)
    }
}

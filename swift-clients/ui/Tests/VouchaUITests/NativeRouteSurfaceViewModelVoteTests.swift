import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
extension NativeRouteSurfaceViewModelTests {
    func testTopicDetailMergesViewerVoteIntoTopicElection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/swift"] = (
            Data("""
            {
              "topic": {
                "id": "topic-1",
                "slug": "swift",
                "name": "Swift",
                "description": "Native topic"
              },
              "topic_election": {
                "votes_score_net": 2,
                "votes_count_up": 5,
                "votes_count_down": 3,
                "my_vote": "dislike"
              },
              "election_vote": {
                "__entity_type": "election_vote",
                "entity_id": "topic-1",
                "user_id": "user-1",
                "choice": "like",
                "created_at": "2026-01-01T00:00:00Z"
              }
            }
            """.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/posts")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.topicDetailId, "topic-1")
        let summary = try XCTUnwrap(viewModel.topicVoteSummary(for: "topic-1"))
        XCTAssertEqual(summary.votesScoreNet, 2)
        XCTAssertEqual(summary.votesCountUp, 5)
        XCTAssertEqual(summary.votesCountDown, 3)
        XCTAssertEqual(summary.myVote, .like)
        let voteRow = try XCTUnwrap(viewModel.rows.dropFirst().first)
        XCTAssertEqual(voteRow.title, "Vote")
        XCTAssertEqual(voteRow.detail, "5 positive votes · 3 negative votes")
    }

    func testTopicVoteUsesOptimisticStateAndRollsBackOnFailure() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/swift"] = (
            Data("""
            {
              "topic": {
                "id": "topic-1",
                "slug": "swift",
                "name": "Swift",
                "description": "Native topic"
              },
              "topic_election": {
                "votes_score_net": 2,
                "votes_count_up": 5,
                "votes_count_down": 3,
                "my_vote": "like"
              },
              "election_vote": {
                "__entity_type": "election_vote",
                "entity_id": "hostname-1",
                "user_id": "user-1",
                "choice": "like",
                "created_at": "2026-01-01T00:00:00Z"
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/vote"] = (Data("{}".utf8), 500)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/posts")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()
        await viewModel.vote(topicId: "topic-1", choice: .dislike)

        XCTAssertEqual(viewModel.myVotesByTopicId["topic-1"], .like)
        XCTAssertEqual(viewModel.topicVoteSummary(for: "topic-1")?.votesCountUp, 5)
        XCTAssertEqual(viewModel.topicVoteSummary(for: "topic-1")?.votesCountDown, 3)
    }

    func testTopicVoteUsesOptimisticStateOnSuccess() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/swift"] = (
            Data("""
            {
              "topic": {
                "id": "topic-1",
                "slug": "swift",
                "name": "Swift",
                "description": "Native topic"
              },
              "topic_election": {
                "votes_score_net": 2,
                "votes_count_up": 5,
                "votes_count_down": 3,
                "my_vote": null
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/vote"] = (Data("{}".utf8), 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/posts")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []

        await viewModel.vote(topicId: "topic-1", choice: .like)

        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.contains("PUT"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/topics/topic-1/vote" })
        XCTAssertEqual(viewModel.myVotesByTopicId["topic-1"], .like)
        XCTAssertEqual(viewModel.topicVoteSummary(for: "topic-1")?.myVote, .like)
        XCTAssertEqual(viewModel.topicVoteSummary(for: "topic-1")?.votesCountUp, 6)
    }

    func testRepeatedTopicVoteWhileWriteInFlightSendsOneRequest() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/swift"] = (
            Data("""
            {
              "topic": {
                "id": "topic-1",
                "slug": "swift",
                "name": "Swift",
                "description": "Native topic"
              },
              "topic_election": {
                "votes_score_net": 2,
                "votes_count_up": 5,
                "votes_count_down": 3,
                "my_vote": null
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/topics/topic-1/vote"] = [
            (Data("{}".utf8), 200, 0.05),
            (Data("{}".utf8), 200, 0)
        ]
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/posts")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()
        async let staleVote: Void = viewModel.vote(topicId: "topic-1", choice: .like)
        try await Task.sleep(for: .milliseconds(10))
        await viewModel.vote(topicId: "topic-1", choice: .dislike)
        await staleVote

        let voteRequests = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/topics/topic-1/vote" }
        XCTAssertEqual(voteRequests.count, 1)
        XCTAssertEqual(viewModel.myVotesByTopicId["topic-1"], .like)
        XCTAssertEqual(viewModel.topicVoteSummary(for: "topic-1")?.myVote, .like)
    }
}

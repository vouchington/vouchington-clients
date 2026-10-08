@testable import VouchaFeatures
import XCTest

@MainActor
final class HostnameDetailVotingTests: NativeRouteSurfaceViewModelTestCase {
    func testHostnameVoteHydratesCountsAndUsesVoteEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.com"] = (detail(vote: nil), 200)
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/hostname-1/vote"] = (Data("{}".utf8), 200)
        let viewModel = try await load()
        let votePath = "/api/v1/hostnames/hostname-1/vote"
        CannedFeedURLProtocol.suspendResponse(path: votePath)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: votePath, method: "PUT")

        let vote = Task { await viewModel.voteHostname(choice: .vouch) }
        _ = try await barrier.wait()

        XCTAssertEqual(viewModel.hostnameVoteSummary()?.myVote, .vouch)
        XCTAssertEqual(viewModel.hostnameVoteSummary()?.votesCountUp, 6)
        XCTAssertEqual(trustVoteDetail(viewModel), "Vouch")
        CannedFeedURLProtocol.releaseResponse(path: votePath)
        await vote.value
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/hostnames/hostname-1/vote" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.contains("PUT"))
    }

    func testHostnameClearRemainsAvailableForExistingVoteAndRollsBackFailedMutation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.com"] = (detail(vote: "vouch"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/hostname-1/vote"] = (Data("{}".utf8), 500)
        let viewModel = try await load()

        await viewModel.voteHostname(choice: nil)

        XCTAssertEqual(viewModel.hostnameVoteSummary()?.myVote, .vouch)
        XCTAssertEqual(viewModel.hostnameVoteSummary()?.votesCountUp, 5)
        XCTAssertEqual(trustVoteDetail(viewModel), "Vouch")
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.contains("DELETE"))
    }

    private func load() async throws -> NativeRouteSurfaceViewModel {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/domain/example.com")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .domainDetail),
            client: makeClient(),
            routeMatch: match
        )
        await viewModel.load()
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        return viewModel
    }

    private func detail(vote: String?) -> Data {
        let electionVote = vote.map {
            "{\"__entity_type\":\"hostname\",\"user_id\":\"user-1\",\"entity_id\":\"hostname-1\",\"choice\":\"\($0)\",\"created_at\":\"2026-01-01T00:00:00Z\"}"
        } ?? "null"
        return Data("""
        {"hostname":{"id":"hostname-1","hostname":"example.com","is_blocked":false},
        "hostname_election":{"votes_score_net":2,"votes_count_up":5,"votes_count_down":3,"my_vote":null},
        "election_vote":\(electionVote),"rss_feeds":[],"top_urls":[],"topic":null}
        """.utf8)
    }

    private func trustVoteDetail(_ viewModel: NativeRouteSurfaceViewModel) -> String? {
        viewModel.rows.first { $0.title == "Trust vote" }?.detail
    }
}

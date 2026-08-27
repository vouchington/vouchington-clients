@testable import VouchaAPI
import VouchaModels
import XCTest

final class ModerationIntegrityPenaltyContractsTests: XCTestCase {
    func testReportPenaltyEndpointsEncodeFiltersAndMutations() {
        let list = Endpoint.reportIntegrityPenalties(
            status: .revoked,
            userId: "user-id",
            sourceFlagId: "flag-id",
            after: "opaque+cursor",
            limit: 25
        )
        assertEndpoint(list, path: "/api/v1/report-integrity/penalties")
        XCTAssertEqual(list.queryItems, [
            .init(name: "limit", value: "25"),
            .init(name: "status", value: "revoked"),
            .init(name: "user_id", value: "user-id"),
            .init(name: "source_flag_id", value: "flag-id"),
            .init(name: "after", value: "opaque+cursor")
        ])
        assertEndpoint(
            .reportIntegrityPenalty(id: "penalty/id"),
            path: "/api/v1/report-integrity/penalties/penalty%2Fid"
        )
        assertEndpoint(
            .revokeReportIntegrityPenalty(id: "penalty-id"),
            method: .DELETE,
            path: "/api/v1/report-integrity/penalties/penalty-id"
        )
    }

    func testVotePenaltyEndpointsAlwaysRequestConfirmedFlagScope() {
        let all = Endpoint.voteIntegrityPenalties()
        XCTAssertEqual(all.queryItems, [.init(name: "source", value: "flag")])
        let active = Endpoint.voteIntegrityPenalties(status: .active, sourceFlagId: "flag-id", limit: 10)
        XCTAssertEqual(active.queryItems, [
            .init(name: "source", value: "flag"),
            .init(name: "limit", value: "10"),
            .init(name: "status", value: "active"),
            .init(name: "source_flag_id", value: "flag-id")
        ])
        assertEndpoint(.voteIntegrityPenalty(id: "penalty-id"), path: "/api/v1/vote-integrity/penalties/penalty-id")
        assertEndpoint(
            .revokeVoteIntegrityPenalty(id: "penalty-id"),
            method: .DELETE,
            path: "/api/v1/vote-integrity/penalties/penalty-id"
        )
    }

    func testReportPenaltyFixturesDecodeEveryLedgerState() throws {
        for id in ["default", "active", "revoked", "all", "page-2"] {
            let response: ReportIntegrityPenaltiesResponse = try decode(
                "native.moderation.report-integrity.penalties.\(id)"
            )
            XCTAssertFalse(response.results.isEmpty)
            if id == "default" {
                XCTAssertTrue(response.results.contains { $0.createdById == nil })
            }
        }
        let exact: ReportIntegrityPenaltyEnvelope = try decode("native.moderation.report-integrity.penalties.get")
        let revoked: ReportIntegrityPenaltyRevokeResponse = try decode(
            "native.moderation.report-integrity.penalties.revoke"
        )
        XCTAssertEqual(exact.penalty.id, revoked.penaltyId)
        XCTAssertEqual(revoked.penalty.userId, revoked.userId)
        XCTAssertNotNil(revoked.penalty.revokedAt)
    }

    func testVotePenaltyFixturesDecodeEveryLedgerStateAndScope() throws {
        for id in ["default", "active", "revoked", "all", "page-2"] {
            let response: VoteIntegrityPenaltiesResponse = try decode(
                "native.moderation.vote-integrity.penalties.\(id)"
            )
            XCTAssertTrue(response.filterScope?.confirmsFlagOnly == true)
            XCTAssertNil(response.filterScope?.sourceFlagId)
            XCTAssertTrue(response.results.allSatisfy { !$0.createdById.isEmpty })
        }
        let exact: VoteIntegrityPenaltyEnvelope = try decode("native.moderation.vote-integrity.penalties.get")
        let revoked: VoteIntegrityPenaltyEnvelope = try decode("native.moderation.vote-integrity.penalties.revoke")
        XCTAssertEqual(exact.penalty.id, revoked.penalty.id)
        XCTAssertNotNil(revoked.penalty.revokedAt)
    }

    private func decode<Value: Decodable>(_ id: String) throws -> Value {
        try JSONDecoder.vouchaFixtureDecoder.decode(Value.self, from: ApiFixtureLoader.data(id))
    }
}

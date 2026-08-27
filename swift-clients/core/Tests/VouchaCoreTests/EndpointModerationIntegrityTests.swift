@testable import VouchaAPI
import VouchaModels
import XCTest

final class EndpointModerationIntegrityTests: XCTestCase {
    func testReportIntegrityEndpointsUseTypedContracts() {
        let list = Endpoint.reportIntegrityFlags(status: .pending, after: "opaque+cursor", limit: 25)
        assertEndpoint(list, path: "/api/v1/report-integrity/flags")
        XCTAssertEqual(list.queryItems, [
            .init(name: "limit", value: "25"),
            .init(name: "status", value: "pending"),
            .init(name: "after", value: "opaque+cursor")
        ])
        assertEndpoint(
            .dismissReportIntegrityFlag(id: "report-flag-1"),
            method: .PATCH,
            path: "/api/v1/report-integrity/flags/report-flag-1",
            body: ["resolution": "dismissed"]
        )
        assertEndpoint(
            .applyReportIntegrityPenalty(flagId: "report-flag-1"),
            method: .POST,
            path: "/api/v1/report-integrity/flags/report-flag-1/penalties"
        )
    }

    func testReportIntegrityFixturesDecodePendingResolvedAndMutationResponses() throws {
        let all = try decode("native.moderation.report-integrity.default", as: ReportIntegrityFlagsResponse.self)
        XCTAssertEqual(all.results.count, 2)
        XCTAssertEqual(all.results.map(\.resolution), [nil, .dismissed])

        let pending = try decode("native.moderation.report-integrity.pending", as: ReportIntegrityFlagsResponse.self)
        XCTAssertNil(pending.results.first?.resolution)
        guard case let .number(windowMinutes)? = pending.results.first?.details["window_minutes"] else {
            return XCTFail("Expected numeric window_minutes evidence.")
        }
        XCTAssertEqual(windowMinutes, 30)

        let resolved = try decode(
            "native.moderation.report-integrity.resolved",
            as: ReportIntegrityFlagsResponse.self
        )
        XCTAssertEqual(resolved.results.first?.resolution, .dismissed)
        XCTAssertEqual(resolved.results.first?.reportedUserId, "user-1")

        let dismissed = try decode(
            "native.moderation.report-integrity.resolution.dismissed",
            as: ReportIntegrityFlagEnvelope.self
        )
        XCTAssertEqual(dismissed.flag.resolution, .dismissed)

        let penalty = try decode(
            "native.moderation.report-integrity.penalty",
            as: ReportIntegrityPenaltyResponse.self
        )
        XCTAssertEqual(penalty.flag.resolution, .penalized)
        XCTAssertEqual(penalty.penalizedUserCount, 2)
        XCTAssertEqual(penalty.penalties.map(\.userId), ["reporter-1", "reporter-2"])
    }

    func testVoteIntegrityEndpointsUseTypedContracts() {
        let list = Endpoint.voteIntegrityFlags(status: .resolved, after: "opaque/cursor", limit: 25)
        assertEndpoint(list, path: "/api/v1/vote-integrity/flags")
        XCTAssertEqual(list.queryItems, [
            .init(name: "limit", value: "25"),
            .init(name: "status", value: "resolved"),
            .init(name: "after", value: "opaque/cursor")
        ])
        for resolution in VoteIntegrityResolution.allCases {
            assertEndpoint(
                .resolveVoteIntegrityFlag(id: "vote-flag-1", resolution: resolution),
                method: .PATCH,
                path: "/api/v1/vote-integrity/flags/vote-flag-1",
                body: ["resolution": resolution.rawValue]
            )
        }
        assertEndpoint(
            .applyVoteIntegrityPenalty(flagId: "vote-flag-1"),
            method: .POST,
            path: "/api/v1/vote-integrity/flags/vote-flag-1/penalties"
        )
    }

    func testVoteIntegrityFixturesDecodeListsAllResolutionsAndCountOnlyPenalty() throws {
        let all = try decode("native.moderation.vote-integrity.default", as: VoteIntegrityFlagsResponse.self)
        XCTAssertEqual(all.results.count, 2)
        XCTAssertEqual(all.results.map(\.resolution), [nil, .dismissed])

        let pending = try decode("native.moderation.vote-integrity.pending", as: VoteIntegrityFlagsResponse.self)
        XCTAssertNil(pending.results.first?.resolution)
        guard case let .number(voteCount)? = pending.results.first?.details["vote_count"] else {
            return XCTFail("Expected numeric vote_count evidence.")
        }
        XCTAssertEqual(voteCount, 20)

        let resolved = try decode(
            "native.moderation.vote-integrity.resolved",
            as: VoteIntegrityFlagsResponse.self
        )
        XCTAssertEqual(resolved.results.first?.resolution, .dismissed)
        XCTAssertEqual(resolved.results.first?.topicId, "topic-1")

        for resolution in VoteIntegrityResolution.allCases {
            let response = try decode(
                "native.moderation.vote-integrity.resolution.\(resolution.rawValue)",
                as: VoteIntegrityFlagEnvelope.self
            )
            XCTAssertEqual(response.flag.resolution, resolution)
        }

        let penalty = try decode(
            "native.moderation.vote-integrity.penalty",
            as: VoteIntegrityPenaltyResponse.self
        )
        XCTAssertEqual(penalty.penalizedUserCount, 2)
    }

    private func decode<Value: Decodable>(_ id: String, as _: Value.Type) throws -> Value {
        try JSONDecoder.vouchaFixtureDecoder.decode(Value.self, from: ApiFixtureLoader.data(id))
    }
}

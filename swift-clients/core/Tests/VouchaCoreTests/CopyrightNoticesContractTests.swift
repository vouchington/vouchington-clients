@testable import VouchaAPI
import VouchaCore
import VouchaModels
import XCTest

final class CopyrightNoticesContractTests: XCTestCase {
    func testAcceptedNoticeListFixturesPreservePopulatedAndNullClaimants() throws {
        let populated = try makeVouchaDecoder().decode(
            CopyrightNoticesResponse.self,
            from: ApiFixtureLoader.data("web.copyright.notices.default")
        )
        let populatedNotice = try XCTUnwrap(populated.copyrightNotices.first)
        XCTAssertEqual(populatedNotice.claimant?.userId, "00000000-0000-7000-8000-000000000806")
        XCTAssertEqual(populatedNotice.claimant?.displayName, "Current claimant")
        XCTAssertEqual(populated.pageInfo.endCursor, "copyright-notices-next")

        let erased = try makeVouchaDecoder().decode(
            CopyrightNoticesResponse.self,
            from: ApiFixtureLoader.data("web.copyright.notices.null-claimant")
        )
        XCTAssertNil(try XCTUnwrap(erased.copyrightNotices.first).claimant)
        XCTAssertNil(erased.pageInfo.endCursor)
    }

    func testPublicDetailFixturesPreservePopulatedAndNullClaimants() throws {
        let populated = try makeVouchaDecoder().decode(
            CopyrightNoticeResponse.self,
            from: ApiFixtureLoader.data("web.copyright.notice.detail.populated")
        )
        XCTAssertEqual(populated.copyrightNotice.claimant?.displayName, "Current claimant")
        XCTAssertEqual(try XCTUnwrap(populated.copyrightNotice.targets?.first).surface, "post-image")

        let erased = try makeVouchaDecoder().decode(
            CopyrightNoticeResponse.self,
            from: ApiFixtureLoader.data("web.copyright.notice.detail.null-claimant")
        )
        XCTAssertNil(erased.copyrightNotice.claimant)
        XCTAssertNotNil(erased.copyrightNotice.provisionalWithholdingAt)
    }

    func testAuthorizedParticipantFixturesPreserveClaimantAndParticipantProjection() throws {
        let populated = try makeVouchaDecoder().decode(
            CopyrightNoticeResponse.self,
            from: ApiFixtureLoader.data("web.copyright.notice.participant.populated")
        ).copyrightNotice
        XCTAssertEqual(populated.claimant?.userId, "00000000-0000-7000-8000-000000000806")
        XCTAssertEqual(populated.viewerRole, "claimant")
        XCTAssertEqual(try XCTUnwrap(populated.statements?.first).deliveryKind, "claimant_decision_notice")

        let erased = try makeVouchaDecoder().decode(
            CopyrightNoticeResponse.self,
            from: ApiFixtureLoader.data("web.copyright.notice.participant.null-claimant")
        ).copyrightNotice
        XCTAssertNil(erased.claimant)
        XCTAssertEqual(erased.viewerRole, "poster")
        XCTAssertEqual(erased.respondableTargetIds, ["00000000-0000-7000-8000-000000000805"])
    }

    func testMissingClaimantKeyFailsInsteadOfBecomingAnErasedProfile() throws {
        let fixture = ApiFixtureLoader.data("web.copyright.notices.default")
        var response = try XCTUnwrap(
            JSONSerialization.jsonObject(with: fixture) as? [String: Any]
        )
        var notices = try XCTUnwrap(response["copyright_notices"] as? [[String: Any]])
        notices[0].removeValue(forKey: "claimant")
        response["copyright_notices"] = notices
        let missingClaimantFixture = try JSONSerialization.data(withJSONObject: response)

        XCTAssertThrowsError(
            try makeVouchaDecoder().decode(CopyrightNoticesResponse.self, from: missingClaimantFixture)
        ) { error in
            guard case let DecodingError.keyNotFound(key, _) = error else {
                return XCTFail("Expected missing claimant to fail with keyNotFound, got \(error)")
            }
            XCTAssertEqual(key.stringValue, "claimant")
        }
    }

    func testAcceptedNoticeEndpointsKeepOpaqueCursorAndEscapeIds() {
        let firstPage = Endpoint.copyrightNotices()
        XCTAssertEqual(firstPage.path, "/api/v1/copyright-notices")
        XCTAssertTrue(firstPage.queryItems.isEmpty)

        let nextPage = Endpoint.copyrightNotices(after: "opaque cursor / page", limit: 40)
        XCTAssertEqual(nextPage.queryItems.map(\.name), ["limit", "after"])
        XCTAssertEqual(nextPage.queryItems.map(\.value), ["40", "opaque cursor / page"])

        assertEndpoint(
            Endpoint.copyrightNotice(id: "notice / one"),
            path: "/api/v1/copyright-notices/notice%20%2F%20one"
        )
        assertEndpoint(
            Endpoint.copyrightParticipantNotice(id: "notice / one"),
            path: "/api/v1/copyright-notices/notice%20%2F%20one/participant"
        )

        let settlements = Endpoint.copyrightEuDisputeSettlements(
            id: "notice / one",
            after: "settlement cursor",
            limit: 25
        )
        XCTAssertEqual(settlements.path, "/api/v1/copyright-notices/notice%20%2F%20one/eu-dispute-settlements")
        XCTAssertEqual(settlements.queryItems.map(\.name), ["limit", "after"])
        XCTAssertEqual(settlements.queryItems.map(\.value), ["25", "settlement cursor"])
    }

    func testEuParticipantAndDisputeSettlementFixturesPreserveReadOnlyHistory() throws {
        let participant = try makeVouchaDecoder().decode(
            CopyrightNoticeResponse.self,
            from: ApiFixtureLoader.data("web.copyright.eu.participant.no-action-complaint")
        ).copyrightNotice
        XCTAssertEqual(participant.eu?.disputeSettlements.first?.bodyName, "Example certified dispute settlement body")
        XCTAssertNil(participant.eu?.disputeSettlements.first?.outcome)
        XCTAssertFalse(try XCTUnwrap(participant.eu?.disputeSettlementsPageInfo).hasNextPage)

        let settlements = try makeVouchaDecoder().decode(
            CopyrightEuDisputeSettlementsResponse.self,
            from: ApiFixtureLoader.data("web.copyright.eu.dispute-settlements.participant")
        )
        XCTAssertTrue(settlements.copyrightEuDisputeSettlements.isEmpty)
        XCTAssertFalse(settlements.pageInfo.hasNextPage)
    }
}

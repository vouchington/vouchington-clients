import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ModerationReportContractsTests: XCTestCase {
    func testStaffFlatFixtureDecodesStaffOnlyContext() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffFlatModerationReportsResponse.self,
            from: ApiFixtureLoader.data("native.moderation.reports.default")
        )
        let report = try XCTUnwrap(response.reports.first)
        XCTAssertEqual(report.reporterUsername, "alice")
        XCTAssertEqual(report.judgement?.recommendedAction, "remove")
        XCTAssertFalse(report.isSystemGenerated)
    }

    func testMemberFlatFixtureDecodesWithoutStaffOnlyContext() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            MemberFlatModerationReportsResponse.self,
            from: ApiFixtureLoader.data("native.moderation.reports.member.default")
        )
        let report = try XCTUnwrap(response.reports.first)
        XCTAssertEqual(report.targetLabel, "Native post")
        XCTAssertEqual(report.status, .pending)
    }

    func testClusteredFixtureDecodesLoadedReports() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ApiFixtureLoader.data("native.moderation.reports.clustered.default")
        )
        XCTAssertEqual(response.clusterMode, "entity")
        let mixed = try XCTUnwrap(response.clusters.first {
            $0.entityType == "user" && $0.entityId == "00000000-0000-7000-8000-000000000201"
        })
        XCTAssertEqual(Set(mixed.reports.map(\.id)), [
            "00000000-0000-7000-8100-000000000201",
            "00000000-0000-7000-8100-000000000202"
        ])
        XCTAssertEqual(mixed.reports.filter(\.isSystemGenerated).count, 1)
        XCTAssertNotNil(mixed.reports.first(where: \.isSystemGenerated)?.communityBanEvasion)
        let duplicate = try XCTUnwrap(response.duplicateClusters.first)
        XCTAssertEqual(duplicate.signal, "content_hash_duplicate")
        XCTAssertEqual(duplicate.clusters.flatMap(\.reports).count, 3)
        XCTAssertEqual(duplicate.reportCount, 3)
    }

    func testResolutionWarningJudgementAndNoContentFixturesDecode() throws {
        let decoder = JSONDecoder.vouchaFixtureDecoder
        let resolution = try decoder.decode(
            ModerationReportResolutionResponse.self,
            from: ApiFixtureLoader.data("native.moderation.report-resolution.reviewed")
        )
        let judgement = try decoder.decode(
            ModerationReportJudgementResponse.self,
            from: ApiFixtureLoader.data("native.moderation.report-judgement.default")
        )
        let warning = try decoder.decode(
            AdminUserWarningResponse.self,
            from: ApiFixtureLoader.data("native.moderation.admin-warning.report")
        )
        XCTAssertEqual(resolution.report.status, .reviewed)
        XCTAssertTrue(judgement.queued)
        XCTAssertEqual(warning.warning.reportId, "019f6559-6b31-7171-b5b1-ccd9d702c45e")
        let confirm = try decoder.decode(
            DecodedJSONValue.self,
            from: ApiFixtureLoader.data("native.moderation.ban-evasion.confirm")
        )
        let dismiss = try decoder.decode(
            DecodedJSONValue.self,
            from: ApiFixtureLoader.data("native.moderation.ban-evasion.dismiss")
        )
        guard case .null = confirm, case .null = dismiss else {
            return XCTFail("Ban-evasion 204 fixtures must have null bodies.")
        }
    }

    func testReportMutationEndpointsEncodeContractBodies() throws {
        assertEndpoint(
            Endpoint.resolveModerationReport(reportId: "report 1", status: .reviewed),
            method: .PATCH,
            path: "/api/v1/reports/report%201",
            body: ["status": "reviewed"]
        )
        try assertEndpoint(
            Endpoint.issueAdminWarning(
                userId: "user-2",
                reason: "Repeated harassment",
                publicMessage: "Stop contacting this user.",
                reportId: "report-1"
            ),
            method: .POST,
            path: "/api/v1/admin/warnings",
            body: [
                "userId": "user-2",
                "reason": "Repeated harassment",
                "publicMessage": "Stop contacting this user.",
                "reportId": "report-1",
                "resolveReport": true
            ]
        )
        XCTAssertThrowsError(try Endpoint.issueAdminWarning(
            userId: "user-2",
            reason: String(repeating: "x", count: 1_001),
            reportId: "report-1"
        ))
        XCTAssertNoThrow(try Endpoint.issueAdminWarning(
            userId: "user-2",
            reason: String(repeating: "😀", count: 500),
            publicMessage: String(repeating: "😀", count: 1_000),
            reportId: "report-1"
        ))
        XCTAssertThrowsError(try Endpoint.issueAdminWarning(
            userId: "user-2",
            reason: String(repeating: "😀", count: 501),
            reportId: "report-1"
        )) { error in
            XCTAssertEqual(error as? AdminWarningValidationError, .reasonTooLong)
        }
        XCTAssertThrowsError(try Endpoint.issueAdminWarning(
            userId: "user-2",
            reason: "Reason",
            publicMessage: String(repeating: "😀", count: 1_001),
            reportId: "report-1"
        )) { error in
            XCTAssertEqual(error as? AdminWarningValidationError, .publicMessageTooLong)
        }
        XCTAssertEqual(
            Endpoint.confirmCommunityBanEvasion(communityIdOrSlug: "native community", userId: "user 2").path,
            "/api/v1/communities/native%20community/ban-evasion/user%202"
        )
        XCTAssertEqual(
            Endpoint.dismissCommunityBanEvasion(communityIdOrSlug: "community-1", userId: "user-2").method,
            .DELETE
        )
    }
}

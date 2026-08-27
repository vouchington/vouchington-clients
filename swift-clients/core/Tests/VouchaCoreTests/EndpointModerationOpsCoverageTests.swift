import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointModerationOpsCoverageTests: XCTestCase {
    func testReportsAndAdminModerationEndpointsUseExpectedRoutes() {
        let reports = Endpoint.moderationReports(status: .pending, after: "cursor-1", limit: 12)
        XCTAssertEqual(reports.path, "/api/v1/reports")
        XCTAssertEqual(reports.queryItems, [
            URLQueryItem(name: "limit", value: "12"),
            URLQueryItem(name: "status", value: "pending"),
            URLQueryItem(name: "after", value: "cursor-1")
        ])

        let clustered = Endpoint.clusteredModerationReports(status: .pending, after: "cursor-2", limit: 12)
        XCTAssertEqual(clustered.path, "/api/v1/reports")
        XCTAssertEqual(clustered.queryItems, [
            URLQueryItem(name: "limit", value: "12"),
            URLQueryItem(name: "status", value: "pending"),
            URLQueryItem(name: "after", value: "cursor-2"),
            URLQueryItem(name: "cluster", value: "entity")
        ])

        let modlog = Endpoint.adminModlog(
            after: "cursor-3",
            communityId: "community-1",
            actorId: "actor-1",
            actionType: "ban",
            limit: 25
        )
        XCTAssertEqual(modlog.path, "/api/v1/admin/modlog")
        XCTAssertEqual(modlog.queryItems, [
            URLQueryItem(name: "limit", value: "25"),
            URLQueryItem(name: "after", value: "cursor-3"),
            URLQueryItem(name: "community_id", value: "community-1"),
            URLQueryItem(name: "actor_id", value: "actor-1"),
            URLQueryItem(name: "action_type", value: "ban")
        ])

        let analytics = Endpoint.adminModerationAnalytics(range: "30d")
        XCTAssertEqual(analytics.path, "/api/v1/admin/moderation-analytics")
        XCTAssertEqual(analytics.queryItems, [URLQueryItem(name: "range", value: "30d")])

        let transparency = Endpoint.moderationTransparency(range: "all", after: "older-page")
        XCTAssertEqual(transparency.path, "/api/v1/moderation-transparency")
        XCTAssertEqual(transparency.queryItems, [
            URLQueryItem(name: "range", value: "all"),
            URLQueryItem(name: "after", value: "older-page")
        ])
    }

    func testIntegrityAndDisputeLifecycleEndpointsUseExpectedRoutes() {
        let reportIntegrityFlags = Endpoint.reportIntegrityFlags(status: .pending, after: "cursor-4", limit: 9)
        XCTAssertEqual(reportIntegrityFlags.path, "/api/v1/report-integrity/flags")
        XCTAssertEqual(reportIntegrityFlags.queryItems, [
            URLQueryItem(name: "limit", value: "9"),
            URLQueryItem(name: "status", value: "pending"),
            URLQueryItem(name: "after", value: "cursor-4")
        ])

        XCTAssertEqual(Endpoint.reportIntegrityFlag(id: "flag-1").path, "/api/v1/report-integrity/flags/flag-1")
        XCTAssertEqual(
            Endpoint.dismissReportIntegrityFlag(id: "flag-1").path,
            "/api/v1/report-integrity/flags/flag-1"
        )
        XCTAssertEqual(
            Endpoint.applyReportIntegrityPenalty(flagId: "flag-1").path,
            "/api/v1/report-integrity/flags/flag-1/penalties"
        )

        let voteIntegrityFlags = Endpoint.voteIntegrityFlags(status: .resolved, after: "cursor-5", limit: 8)
        XCTAssertEqual(voteIntegrityFlags.path, "/api/v1/vote-integrity/flags")
        XCTAssertEqual(voteIntegrityFlags.queryItems, [
            URLQueryItem(name: "limit", value: "8"),
            URLQueryItem(name: "status", value: "resolved"),
            URLQueryItem(name: "after", value: "cursor-5")
        ])
        XCTAssertEqual(Endpoint.voteIntegrityFlag(id: "flag-2").path, "/api/v1/vote-integrity/flags/flag-2")
        XCTAssertEqual(
            Endpoint.applyVoteIntegrityPenalty(flagId: "flag-2").path,
            "/api/v1/vote-integrity/flags/flag-2/penalties"
        )

        XCTAssertEqual(Endpoint.dispute(id: "dispute-1").path, "/api/v1/disputes/dispute-1")
        XCTAssertEqual(Endpoint.updateDisputeDraft(id: "dispute-1").path, "/api/v1/disputes/dispute-1")
        XCTAssertEqual(Endpoint.disputeApproval(id: "dispute-1").path, "/api/v1/disputes/dispute-1/approval")
        XCTAssertEqual(Endpoint.disputeDelivery(id: "dispute-1").path, "/api/v1/disputes/dispute-1/delivery")
        XCTAssertEqual(
            Endpoint.disputeResolution(id: "dispute-1", action: .dismiss).path,
            "/api/v1/disputes/dispute-1/resolution"
        )
        assertEndpoint(
            Endpoint.disputeResolution(
                id: "dispute-1",
                action: .annotate,
                bodyText: "Context annotation"
            ),
            method: .POST,
            path: "/api/v1/disputes/dispute-1/resolution",
            body: ["action": "annotate", "body_text": "Context annotation"]
        )
        XCTAssertEqual(
            Endpoint.disputeResolutionDrafts(id: "dispute-1").path,
            "/api/v1/disputes/dispute-1/resolution-drafts"
        )
    }
}

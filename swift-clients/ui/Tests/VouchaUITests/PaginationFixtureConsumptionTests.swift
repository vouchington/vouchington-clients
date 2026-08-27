import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class PaginationFixtureConsumptionTests: XCTestCase {
    func testFinalizedPaginationFixturesDecodeForUISurfaces() throws {
        let decoder = JSONDecoder.vouchaFixtureDecoder

        let emails = try decoder.decode(
            EmailAddressListResponse.self,
            from: ApiFixtureLoader.data("native.my.email-addresses.empty")
        )
        XCTAssertFalse(emails.pageInfo.hasNextPage)

        let sessions = try decoder.decode(
            Page<AuthSession>.self,
            from: ApiFixtureLoader.data("native.auth.sessions.default")
        )
        XCTAssertEqual(sessions.results.count, 2)

        let apiKeys = try decoder.decode(
            Page<ApiKey>.self,
            from: ApiFixtureLoader.data("native.my.api-keys.paginated")
        )
        XCTAssertTrue(apiKeys.pageInfo.hasNextPage)

        let push = try decoder.decode(
            Page<WebPushSubscription>.self,
            from: ApiFixtureLoader.data("native.my.push-subscriptions.paginated")
        )
        XCTAssertTrue(push.pageInfo.hasNextPage)

        let relations = try decoder.decode(
            EntityRelationsResponse.self,
            from: ApiFixtureLoader.data("native.entity-relations.post.category.topic.default")
        )
        XCTAssertEqual(relations.results.count, 1)

        let descendants = try decoder.decode(
            PostThreadEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.descendants.default")
        )
        XCTAssertEqual(descendants.results.count, 4)

        let reports = try decoder.decode(
            CommunityPendingReportsResponse.self,
            from: ApiFixtureLoader.data("native.community.pending-reports.paginated")
        )
        let report = try XCTUnwrap(reports.reports.first)
        XCTAssertEqual(reports.reports.count, 1)
        XCTAssertEqual(report.id, "00000000-0000-7000-8000-000000000601")
        XCTAssertEqual(report.status, .pending)
        XCTAssertNil(report.reviewedAt)
        XCTAssertNil(report.resolvedById)
        XCTAssertEqual(report.claim?.reportId, report.id)
        XCTAssertEqual(report.escalatedById, "00000000-0000-7000-8000-000000000611")
    }
}

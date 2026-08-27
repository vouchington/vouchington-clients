import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationReportsRoundThreeTests: NativeRouteSurfaceViewModelTestCase {
    func testGroupedPaginationMergesRecurringDuplicateClusterSidecars() async throws {
        let firstPage = ApiFixtureLoader.data("native.moderation.reports.clustered.default")
        let overlappingSecondPage = try overlappingSecondPage(firstPage: firstPage)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/reports"] = [
            (firstPage, 200, 0),
            (overlappingSecondPage, 200, 0),
            (firstPage, 200, 0),
            (overlappingSecondPage, 200, 0)
        ]
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)

        await viewModel.load()
        await viewModel.loadMore()

        let duplicate = try XCTUnwrap(viewModel.duplicateClusters.first)
        XCTAssertEqual(duplicate.clusters.count, 6)
        XCTAssertEqual(Set(duplicate.clusters.map(\.id)).count, 6)
        XCTAssertEqual(duplicate.postCount, 6)
        XCTAssertEqual(duplicate.reportCount, 10)
        XCTAssertEqual(duplicate.reasonBreakdown.count, 1)
        let reason = try XCTUnwrap(duplicate.reasonBreakdown.first)
        XCTAssertEqual(reason.reason, "spam")
        XCTAssertEqual(reason.count, 10)
        XCTAssertEqual(duplicate.firstReportedAt, duplicate.clusters.map(\.firstReportedAt).min())
        XCTAssertEqual(duplicate.lastReportedAt, duplicate.clusters.map(\.lastReportedAt).max())
        XCTAssertFalse(viewModel.hasNextPage)

        let report = try XCTUnwrap(viewModel.clusters.flatMap(\.reports).first {
            viewModel.actionIsAllowed(.dismiss, for: $0)
        })
        CannedFeedURLProtocol.handlers["/api/v1/reports/\(report.id)"] =
            (ModerationReportsTestFixtures.resolution, 200)
        await viewModel.perform(.dismiss, reportId: report.id)

        let refreshedDuplicate = try XCTUnwrap(viewModel.duplicateClusters.first)
        XCTAssertEqual(refreshedDuplicate.clusters.count, 6)
        XCTAssertEqual(Set(refreshedDuplicate.clusters.map(\.id)).count, 6)
        XCTAssertEqual(refreshedDuplicate.postCount, 6)
        XCTAssertEqual(refreshedDuplicate.reportCount, 10)
        XCTAssertEqual(refreshedDuplicate.reasonBreakdown.count, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/reports"), 4)
    }

    func testSurfaceIdentityChangesForAuthenticationClientAndRole() throws {
        let firstClient = try makeClient()
        let secondClient = try makeClient()
        let signedOut = ModerationReportsSurfaceIdentity(
            isSignedIn: false, client: nil, viewerTier: .member
        )
        let signedIn = ModerationReportsSurfaceIdentity(
            isSignedIn: true, client: firstClient, viewerTier: .member
        )

        XCTAssertNotEqual(signedOut, signedIn)
        XCTAssertNotEqual(
            signedIn,
            ModerationReportsSurfaceIdentity(
                isSignedIn: true, client: secondClient, viewerTier: .member
            )
        )
        XCTAssertNotEqual(
            signedIn,
            ModerationReportsSurfaceIdentity(
                isSignedIn: true, client: firstClient, viewerTier: .administrator
            )
        )
    }

    func testDuplicateWaveSelectionReplacesUnrelatedSelectionWithEligibleReports() async throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["wave", "system"], systemIds: ["system"]
            )
        )
        let wave = try XCTUnwrap(response.duplicateClusters.first)
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        viewModel.duplicateClusters = [wave]
        viewModel.selectedReportIds = ["unrelated"]

        viewModel.replaceSelectionWithActiveRemovableReports(in: wave)

        XCTAssertEqual(viewModel.selectedReportIds, ["wave"])
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-wave"] = (Data(), 204)
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = try (
            ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["system"], systemIds: ["system"]
            ),
            200
        )
        await viewModel.bulkRemoveSelected()
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/posts/post-wave"), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/posts/post-unrelated"), 0)
    }

    func testGroupedBulkDismissRefreshesAndPreservesFailedSkippedState() async throws {
        let initial = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.cluster(
                reportIds: ["ok", "failed", "system"], systemIds: ["system"]
            )
        )
        CannedFeedURLProtocol.handlers["/api/v1/reports/ok"] = (
            ModerationReportsTestFixtures.resolution, 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/reports/failed"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = try (
            ModerationReportsTestFixtures.cluster(
                reportIds: ["failed", "system"], systemIds: ["system"]
            ),
            200
        )
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .siteModerator)
        viewModel.clusters = initial.clusters
        viewModel.selectedReportIds = ["ok", "failed", "system"]

        await viewModel.bulkDismissSelected()

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/reports"), 1)
        XCTAssertNil(viewModel.report(id: "ok"))
        XCTAssertNotNil(viewModel.report(id: "failed"))
        XCTAssertNotNil(viewModel.report(id: "system"))
        XCTAssertEqual(viewModel.selectedReportIds, ["failed", "system"])
        XCTAssertNotNil(viewModel.actionErrors["failed"])
        XCTAssertNotNil(viewModel.actionErrors["system"])
    }

    private func overlappingSecondPage(firstPage: Data) throws -> Data {
        let firstRoot = try XCTUnwrap(
            try JSONSerialization.jsonObject(with: firstPage) as? [String: Any]
        )
        var secondRoot = try XCTUnwrap(
            try JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.moderation.reports.clustered.page-2")
            ) as? [String: Any]
        )
        let firstDuplicate = try XCTUnwrap((firstRoot["duplicate_clusters"] as? [[String: Any]])?.first)
        let overlap = try XCTUnwrap((firstDuplicate["clusters"] as? [[String: Any]])?.first)
        var duplicates = try XCTUnwrap(secondRoot["duplicate_clusters"] as? [[String: Any]])
        var clusters = try XCTUnwrap(duplicates[0]["clusters"] as? [[String: Any]])
        clusters.append(overlap)
        duplicates[0]["clusters"] = clusters
        secondRoot["duplicate_clusters"] = duplicates
        return try JSONSerialization.data(withJSONObject: secondRoot, options: [.sortedKeys])
    }
}

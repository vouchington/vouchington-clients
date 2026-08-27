import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationReportsViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testMemberLoadsRedactedFlatPendingNewestAndCannotMutate() async throws {
        CannedFeedURLProtocol.handlers[reportsPath] = (ModerationReportsTestFixtures.memberFlat, 200)
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .member)

        await viewModel.load()
        viewModel.toggleSelection("019f6559-6b31-7171-b5b1-ccd9d702c45e")
        await viewModel.perform(.dismiss, reportId: "019f6559-6b31-7171-b5b1-ccd9d702c45e")

        XCTAssertEqual(viewModel.memberReports.map(\.id), ["019f6559-6b31-7171-b5b1-ccd9d702c45e"])
        XCTAssertTrue(viewModel.isReadOnly)
        XCTAssertTrue(viewModel.selectedReportIds.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=25&status=pending&sort=created_at_desc")
    }

    func testStaffLoadsGroupedPendingNewestAndFallsBackToFlat() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = [
            (Data("{}".utf8), 500, 0),
            (ModerationReportsTestFixtures.staffFlat, 200, 0)
        ]
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .siteModerator)

        await viewModel.load()

        XCTAssertEqual(viewModel.mode, .flat)
        XCTAssertEqual(viewModel.staffReports.map(\.id), ["019f6559-6b31-7171-b5b1-ccd9d702c45e"])
        XCTAssertEqual(viewModel.notice, "Grouped reports are unavailable. Showing the flat queue.")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25&status=pending&cluster=entity")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=25&status=pending&sort=severity")
    }

    func testChangingFilterModeOrSortResetsSelectionAndCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = try [
            (ModerationReportsTestFixtures.cluster(reportIds: ["r1"], hasNextPage: true, endCursor: "c1"), 200, 0),
            (ModerationReportsTestFixtures.cluster(reportIds: ["r1"]), 200, 0),
            (ModerationReportsTestFixtures.staffFlat(ids: ["r1"]), 200, 0),
            (ModerationReportsTestFixtures.staffFlat(ids: ["r1"]), 200, 0)
        ]
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        await viewModel.load()
        viewModel.toggleSelection("r1")

        await viewModel.updateStatus(.reviewed)
        XCTAssertTrue(viewModel.selectedReportIds.isEmpty)
        XCTAssertNil(viewModel.endCursor)
        await viewModel.updateMode(.flat)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=25&status=reviewed&sort=severity")
        await viewModel.updateSort(.mostReported)

        XCTAssertEqual(viewModel.sort, .mostReported)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=25&status=reviewed&sort=most_reported")
    }

    func testPaginationAppendsAndDeduplicatesWhilePreservingRowsOnFailure() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = try [
            (ModerationReportsTestFixtures.staffFlat(
                ids: ["r1"], hasNextPage: true, endCursor: "c1"
            ), 200, 0),
            (ModerationReportsTestFixtures.staffFlat(
                ids: ["r1", "r2"], hasNextPage: true, endCursor: "c2"
            ), 200, 0)
        ]
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        await viewModel.updateMode(.flat)
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.staffReports.map(\.id), ["r1", "r2"])
        XCTAssertEqual(viewModel.endCursor, "c2")

        CannedFeedURLProtocol.errors[reportsPath] = URLError(.timedOut)
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.staffReports.map(\.id), ["r1", "r2"])
        XCTAssertEqual(viewModel.endCursor, "c2")
        XCTAssertNotNil(viewModel.paginationError)
    }

    func testStaleLoadCannotReplaceNewerFilterResults() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = try [
            (ModerationReportsTestFixtures.cluster(reportIds: ["stale"]), 200, 0.08),
            (ModerationReportsTestFixtures.staffFlat(ids: ["current"]), 200, 0)
        ]
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)

        let stale = Task { await viewModel.load() }
        try await Task.sleep(for: .milliseconds(10))
        await viewModel.updateMode(.flat)
        await stale.value

        XCTAssertEqual(viewModel.staffReports.map(\.id), ["current"])
        XCTAssertTrue(viewModel.clusters.isEmpty)
    }

    func testReviewDismissRerunWarningAndBanEvasionActionsUseExpectedContracts() async throws {
        CannedFeedURLProtocol.handlers[reportsPath] = try (
            ModerationReportsTestFixtures.staffFlat(ids: ["review", "dismiss", "rerun", "warn"]), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/reports/review"] = (ModerationReportsTestFixtures.resolution, 200)
        CannedFeedURLProtocol.handlers["/api/v1/reports/dismiss"] = (ModerationReportsTestFixtures.resolution, 200)
        CannedFeedURLProtocol.handlers["/api/v1/reports/rerun/judgements"] = (
            ModerationReportsTestFixtures.judgement, 202
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/warnings"] = (ModerationReportsTestFixtures.warning, 201)
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        await viewModel.updateMode(.flat)

        await viewModel.perform(.review, reportId: "review")
        await viewModel.perform(.dismiss, reportId: "dismiss")
        await viewModel.perform(.rerunJudgement, reportId: "rerun")
        await viewModel.perform(.warn(reason: "Repeated harassment", publicMessage: "Stop."), reportId: "warn")

        XCTAssertEqual(viewModel.staffReports.map(\.id), ["rerun"])
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.contains("PATCH"))
        let warningBody = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last.flatMap { $0 })
        XCTAssertTrue(warningBody.contains(#""resolveReport":true"#))
        XCTAssertFalse(warningBody.contains("communityId"))
    }

    func testBanEvasionActionsUseDedicatedNoContentEndpoints() async throws {
        CannedFeedURLProtocol.handlers[reportsPath] = try (
            ModerationReportsTestFixtures.staffFlat(ids: ["confirm", "dismiss"], systemIds: ["confirm", "dismiss"]),
            200
        )
        let path = "/api/v1/communities/community-1/ban-evasion/user-2"
        CannedFeedURLProtocol.handlers[path] = (Data(), 204)
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .siteModerator)
        await viewModel.updateMode(.flat)

        await viewModel.perform(.confirmBanEvasion, reportId: "confirm")
        await viewModel.perform(.dismissBanEvasion, reportId: "dismiss")

        XCTAssertTrue(viewModel.staffReports.isEmpty)
        XCTAssertEqual(Array(CannedFeedURLProtocol.capturedMethods.suffix(2)), ["POST", "DELETE"])
    }

    func testBulkDismissKeepsFailedAndSystemReportsSelected() async throws {
        CannedFeedURLProtocol.handlers[reportsPath] = try (
            ModerationReportsTestFixtures.staffFlat(ids: ["ok", "failed", "system"], systemIds: ["system"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/reports/ok"] = (ModerationReportsTestFixtures.resolution, 200)
        CannedFeedURLProtocol.handlers["/api/v1/reports/failed"] = (Data("{}".utf8), 500)
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .siteModerator)
        await viewModel.updateMode(.flat)
        ["ok", "failed", "system"].forEach(viewModel.toggleSelection)

        await viewModel.bulkDismissSelected()

        XCTAssertEqual(Set(viewModel.staffReports.map(\.id)), ["failed", "system"])
        XCTAssertEqual(viewModel.selectedReportIds, ["failed", "system"])
        XCTAssertNotNil(viewModel.actionErrors["failed"])
        XCTAssertNotNil(viewModel.actionErrors["system"])
    }

    func testBulkRemoveDeduplicatesTargetsAndKeepsFailedReportsSelected() async throws {
        CannedFeedURLProtocol.handlers[reportsPath] = try (
            ModerationReportsTestFixtures.staffFlat(
                ids: ["a", "b", "c"], targetIds: ["a": "post-one", "b": "post-one", "c": "post-two"]
            ), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-one"] = (Data(), 204)
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-two"] = (Data("{}".utf8), 500)
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        await viewModel.updateMode(.flat)
        ["a", "b", "c"].forEach(viewModel.toggleSelection)

        await viewModel.bulkRemoveSelected()

        XCTAssertEqual(viewModel.staffReports.map(\.id), ["c"])
        XCTAssertEqual(viewModel.selectedReportIds, ["c"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/posts/post-one" }.count, 1)
    }

    func testClusterMutationSkipsBanEvasionAndRefreshesLoadedCluster() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = try [
            (ModerationReportsTestFixtures.partialCluster(
                reportIds: ["ordinary", "system"], systemIds: ["system"],
                reportCount: 8, reporterCount: 7, reasons: ["spam": 5, "other": 3]
            ), 200, 0),
            (ModerationReportsTestFixtures.partialCluster(
                reportIds: ["system"], systemIds: ["system"],
                reportCount: 7, reporterCount: 6, reasons: ["spam": 4, "other": 3]
            ), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/reports/ordinary"] = (
            ModerationReportsTestFixtures.resolution, 200
        )
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .siteModerator)
        await viewModel.load()
        let cluster = try XCTUnwrap(viewModel.clusters.first)
        ["ordinary", "system"].forEach(viewModel.toggleSelection)

        await viewModel.dismissLoadedReports(in: cluster.id)

        let refreshed = try XCTUnwrap(viewModel.clusters.first)
        XCTAssertEqual(viewModel.activeReports(in: refreshed).map(\.id), ["system"])
        XCTAssertEqual(refreshed.reportCount, 7)
        XCTAssertEqual(refreshed.reporterCount, 6)
        XCTAssertEqual(
            Dictionary(uniqueKeysWithValues: refreshed.reasonBreakdown.map { ($0.reason, $0.count) }),
            ["spam": 4, "other": 3]
        )
        XCTAssertEqual(viewModel.selectedReportIds, ["system"])
        XCTAssertNotNil(viewModel.actionErrors["system"])
    }

    func testGroupedRefreshPreservesPageDepthAndLaterPageFailedSkippedReports() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = try [
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["success"], duplicateId: "wave-1", hasNextPage: true, endCursor: "page-2"
            ), 200, 0),
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["failed", "skipped"], reportCount: 5, duplicateId: "wave-2",
                systemIds: ["skipped"]
            ), 200, 0),
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: [], reportCount: 4, duplicateId: "wave-1",
                hasNextPage: true, endCursor: "refresh-page-2"
            ), 200, 0),
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["failed", "skipped"], reportCount: 5, duplicateId: "wave-2",
                systemIds: ["skipped"]
            ), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-success"] = (Data(), 204)
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-failed"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/reports/failed/judgements"] = (
            ModerationReportsTestFixtures.judgement, 202
        )
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        await viewModel.load()
        await viewModel.loadMore()

        ["success", "failed", "skipped"].forEach(viewModel.toggleSelection)
        await viewModel.bulkRemoveSelected()
        await viewModel.perform(.rerunJudgement, reportId: "failed")

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/reports/failed/judgements"
        })
        XCTAssertNil(viewModel.report(id: "success"))
        XCTAssertNotNil(viewModel.report(id: "failed"))
        XCTAssertNotNil(viewModel.report(id: "skipped"))
        XCTAssertEqual(viewModel.selectedReportIds, ["failed", "skipped"])
        XCTAssertEqual(viewModel.groupedLoadedPageCount, 2)
        XCTAssertEqual(viewModel.duplicateClusters.first(where: { $0.id == "wave-2" })?.reportCount, 5)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == reportsPath }.map(\.query), [
            "limit=25&status=pending&cluster=entity",
            "limit=25&status=pending&after=page-2&cluster=entity",
            "limit=25&status=pending&cluster=entity",
            "limit=25&status=pending&after=refresh-page-2&cluster=entity"
        ])
    }

    func testFailedGroupedRefreshKeepsSuccessHiddenUntilStatusScopeChanges() async throws {
        CannedFeedURLProtocol.handlers[reportsPath] = try (
            ModerationReportsTestFixtures.cluster(reportIds: ["same-id"]), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/reports/same-id"] = (
            ModerationReportsTestFixtures.resolution, 200
        )
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .siteModerator)
        await viewModel.load()
        CannedFeedURLProtocol.errors[reportsPath] = URLError(.timedOut)

        await viewModel.perform(.dismiss, reportId: "same-id")

        XCTAssertNil(viewModel.report(id: "same-id"))
        let staleCluster = try XCTUnwrap(viewModel.clusters.first)
        XCTAssertTrue(viewModel.activeReports(in: staleCluster).isEmpty)

        CannedFeedURLProtocol.errors.removeValue(forKey: reportsPath)
        CannedFeedURLProtocol.handlers[reportsPath] = try (
            ModerationReportsTestFixtures.cluster(
                reportIds: ["same-id"], statuses: ["same-id": "dismissed"]
            ), 200
        )
        await viewModel.updateStatus(.dismissed)

        XCTAssertEqual(viewModel.report(id: "same-id")?.status, .dismissed)
        let dismissedCluster = try XCTUnwrap(viewModel.clusters.first)
        XCTAssertEqual(viewModel.activeReports(in: dismissedCluster).map(\.id), ["same-id"])
    }

    func testSuccessfulAuthoritativeRefreshAcceptsReturnedReportAndClearsTombstone() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = try [
            (ModerationReportsTestFixtures.cluster(reportIds: ["accepted"]), 200, 0),
            (ModerationReportsTestFixtures.cluster(reportIds: ["accepted"]), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/reports/accepted"] = (
            ModerationReportsTestFixtures.resolution, 200
        )
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .siteModerator)
        await viewModel.load()

        await viewModel.perform(.dismiss, reportId: "accepted")

        XCTAssertNotNil(viewModel.report(id: "accepted"))
        let acceptedCluster = try XCTUnwrap(viewModel.clusters.first)
        XCTAssertEqual(viewModel.activeReports(in: acceptedCluster).map(\.id), ["accepted"])
    }

    func testActionRefreshInvalidatesOverlappingLoadMoreAndRecoversControls() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = try [
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["success"], duplicateId: "page-1", hasNextPage: true, endCursor: "page-2"
            ), 200, 0),
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["failed", "skipped"], duplicateId: "page-2", systemIds: ["skipped"],
                hasNextPage: true, endCursor: "page-3"
            ), 200, 0),
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["stale"], duplicateId: "stale-page", hasNextPage: true, endCursor: "stale-cursor"
            ), 200, 0.1),
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: [], reportCount: 4, duplicateId: "page-1",
                hasNextPage: true, endCursor: "refresh-page-2"
            ), 200, 0.05),
            (ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["failed", "skipped"], reportCount: 5, duplicateId: "page-2",
                systemIds: ["skipped"]
            ), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-success"] = (Data(), 204)
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        await viewModel.load()
        await viewModel.loadMore()
        ["success", "failed", "skipped"].forEach(viewModel.toggleSelection)

        let staleLoad = Task { await viewModel.loadMore() }
        try await Task.sleep(for: .milliseconds(10))
        let action = Task { await viewModel.perform(.removeTarget, reportId: "success") }
        try await waitForReportRequests(count: 4)

        XCTAssertTrue(viewModel.isRefreshingGrouped)
        XCTAssertFalse(viewModel.isLoadingMore)
        XCTAssertFalse(viewModel.canLoadMore)
        await viewModel.updateStatus(.reviewed)
        await viewModel.updateMode(.flat)
        await viewModel.updateSort(.severity)
        await viewModel.perform(.rerunJudgement, reportId: "failed")
        await viewModel.bulkRemoveSelected()

        await action.value
        await staleLoad.value

        XCTAssertFalse(viewModel.isRefreshingGrouped)
        XCTAssertFalse(viewModel.isLoadingMore)
        XCTAssertEqual(viewModel.mode, .grouped)
        XCTAssertEqual(viewModel.status, .pending)
        XCTAssertEqual(viewModel.sort, .severity)
        XCTAssertEqual(viewModel.groupedLoadedPageCount, 2)
        XCTAssertNil(viewModel.endCursor)
        XCTAssertFalse(viewModel.hasNextPage)
        XCTAssertNil(viewModel.report(id: "success"))
        XCTAssertNil(viewModel.report(id: "stale"))
        XCTAssertNotNil(viewModel.report(id: "failed"))
        XCTAssertNotNil(viewModel.report(id: "skipped"))
        XCTAssertEqual(viewModel.selectedReportIds, ["failed", "skipped"])
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/reports/failed/judgements"
        })
    }

    func testQueueWideGateRejectsOverlappingRowMutationAndRecovers() async throws {
        CannedFeedURLProtocol.queuedHandlers[reportsPath] = try [
            (ModerationReportsTestFixtures.cluster(reportIds: ["first", "second"]), 200, 0),
            (ModerationReportsTestFixtures.cluster(reportIds: ["second"]), 200, 0),
            (ModerationReportsTestFixtures.cluster(reportIds: []), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/reports/first"] = [
            (ModerationReportsTestFixtures.resolution, 200, 0.08)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/reports/second"] = (
            ModerationReportsTestFixtures.resolution, 200
        )
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .siteModerator)
        await viewModel.load()

        let first = Task { await viewModel.perform(.dismiss, reportId: "first") }
        try await waitForRequest(path: "/api/v1/reports/first")
        XCTAssertTrue(viewModel.isMutatingQueue)
        XCTAssertTrue(viewModel.isQueueActionInProgress)
        await viewModel.perform(.dismiss, reportId: "second")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/reports/second"), 0)

        await first.value
        XCTAssertFalse(viewModel.isQueueActionInProgress)
        XCTAssertTrue(viewModel.inFlightReportIds.isEmpty)
        XCTAssertNotNil(viewModel.report(id: "second"))

        await viewModel.perform(.dismiss, reportId: "second")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/reports/second"), 1)
        XCTAssertFalse(viewModel.isQueueActionInProgress)
        XCTAssertNil(viewModel.report(id: "second"))
    }

    func testActionEligibilityRequiresPendingAndHonorsEveryViewerTier() throws {
        let data = try ModerationReportsTestFixtures.staffFlat(
            ids: [
                "pending", "reviewed", "actioned", "dismissed", "system-pending", "system-reviewed",
                "system-actioned", "system-dismissed"
            ],
            systemIds: ["system-pending", "system-reviewed", "system-actioned", "system-dismissed"],
            statuses: [
                "reviewed": "reviewed", "actioned": "actioned", "dismissed": "dismissed",
                "system-reviewed": "reviewed", "system-actioned": "actioned", "system-dismissed": "dismissed"
            ]
        )
        let reports = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffFlatModerationReportsResponse.self,
            from: data
        ).reports
        let byId = Dictionary(uniqueKeysWithValues: reports.map { ($0.id, $0) })
        let resolvingActions: [ModerationReportAction] = [
            .review, .dismiss, .warn(reason: "Reason", publicMessage: nil), .removeTarget
        ]

        for tier in [ModerationReportsViewerTier.member, .siteModerator, .administrator] {
            let viewModel = ModerationReportsViewModel(client: nil, viewerTier: tier)
            for statusId in ["reviewed", "actioned", "dismissed"] {
                let report = try XCTUnwrap(byId[statusId])
                let systemReport = try XCTUnwrap(byId["system-\(statusId)"])
                XCTAssertEqual(viewModel.actionIsAllowed(.rerunJudgement, for: report), tier.isStaff)
                for action in resolvingActions + [.confirmBanEvasion, .dismissBanEvasion] {
                    XCTAssertFalse(viewModel.actionIsAllowed(action, for: report))
                    XCTAssertFalse(viewModel.actionIsAllowed(action, for: systemReport))
                }
            }
            let pending = try XCTUnwrap(byId["pending"])
            XCTAssertEqual(viewModel.actionIsAllowed(.rerunJudgement, for: pending), tier.isStaff)
            XCTAssertEqual(viewModel.actionIsAllowed(.review, for: pending), tier.isStaff)
            XCTAssertEqual(viewModel.actionIsAllowed(.dismiss, for: pending), tier.isStaff)
            XCTAssertEqual(
                viewModel.actionIsAllowed(.warn(reason: "Reason", publicMessage: nil), for: pending),
                tier.isStaff
            )
            XCTAssertEqual(viewModel.actionIsAllowed(.removeTarget, for: pending), tier == .administrator)
            let system = try XCTUnwrap(byId["system-pending"])
            XCTAssertEqual(viewModel.actionIsAllowed(.confirmBanEvasion, for: system), tier.isStaff)
            XCTAssertEqual(viewModel.actionIsAllowed(.dismissBanEvasion, for: system), tier.isStaff)
            XCTAssertFalse(viewModel.actionIsAllowed(.review, for: system))
            XCTAssertFalse(viewModel.actionIsAllowed(.warn(reason: "Reason", publicMessage: nil), for: system))
            XCTAssertFalse(viewModel.actionIsAllowed(.removeTarget, for: system))
        }
    }

    func testLoadedReportDeduplicationPreservesFirstSeenOrder() throws {
        let top = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.cluster(reportIds: ["shared", "top"])
        )
        let duplicate = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.nestedDuplicate(reportIds: ["shared", "duplicate"])
        )
        let viewModel = ModerationReportsViewModel(client: nil, viewerTier: .siteModerator)
        viewModel.clusters = Array(top.clusters.prefix(1))
        viewModel.duplicateClusters = duplicate.duplicateClusters

        XCTAssertEqual(viewModel.allLoadedStaffReports().map(\.id), ["shared", "top", "duplicate"])
    }

    private func waitForReportRequests(count: Int) async throws {
        for _ in 0 ..< 100 {
            if CannedFeedURLProtocol.capturedPathCount(reportsPath) >= count {
                return
            }
            try await Task.sleep(for: .milliseconds(5))
        }
        XCTFail("Timed out waiting for report requests.")
    }

    private func waitForRequest(path: String) async throws {
        for _ in 0 ..< 100 {
            if CannedFeedURLProtocol.capturedPathCount(path) > 0 {
                return
            }
            try await Task.sleep(for: .milliseconds(5))
        }
        XCTFail("Timed out waiting for \(path).")
    }

    private let reportsPath = "/api/v1/reports"
}

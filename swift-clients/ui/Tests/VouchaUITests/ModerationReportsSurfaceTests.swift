import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class ModerationReportsSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testSignedOutReportsSurfaceRequiresSignIn() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/reports"))
        let sut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: false
        )

        XCTAssertNoThrow(try sut.inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Sign in"))
    }

    func testReportsDirectRouteIsAuthenticatedWhileDiscoveryIsAdminOnly() {
        XCTAssertNotNil(NativeRouteCatalog.matchingRoute(for: "/reports"))
        let memberGroups = AppSection.moderation.nativeParityGroups(isSignedIn: true, userRoles: [])
        let adminGroups = AppSection.moderation.nativeParityGroups(
            isSignedIn: true,
            userRoles: ["administrator"]
        )
        XCTAssertFalse(memberGroups.flatMap(\.entries).contains { $0.representativePath == "/reports" })
        XCTAssertTrue(adminGroups.flatMap(\.entries).contains { $0.representativePath == "/reports" })
    }

    func testMemberSurfaceRendersReadOnlyContext() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            MemberFlatModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.memberFlat
        )
        let report = try XCTUnwrap(response.reports.first)
        let sut = MemberModerationReportCard(report: report, onNavigate: { _ in })
        let inspection = try sut.inspect()

        XCTAssertNoThrow(try inspection.find(text: "Native post"))
        XCTAssertNoThrow(try inspection.find(text: "3 reports"))
        XCTAssertNoThrow(try inspection.find(text: "Reason: spam"))
        XCTAssertThrowsError(try inspection.find(button: "Dismiss"))
    }

    func testStaffSurfaceRendersFiltersContextAndActions() async throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffFlatModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.staffFlat
        )
        let report = try XCTUnwrap(response.reports.first)
        let viewModel = ModerationReportsViewModel(client: nil, viewerTier: .administrator)
        let card = StaffModerationReportCard(
            report: report,
            viewModel: viewModel,
            onNavigate: { _ in },
            onConfirm: { _ in }
        )
        let inspection = try card.inspect()

        XCTAssertNoThrow(try inspection.find(text: "Reporter: alice"))
        XCTAssertNoThrow(try inspection.find(text: "Note: Looks automated."))
        XCTAssertNoThrow(try inspection.find(text: "AI judgement: remove"))
        XCTAssertNoThrow(try inspection.find(button: "Review"))
        XCTAssertNoThrow(try inspection.find(button: "Dismiss"))
        XCTAssertNoThrow(try inspection.find(button: "Warn user"))
        XCTAssertNoThrow(try inspection.find(button: "Remove content"))

        await viewModel.updateMode(.flat)
        let filters = ModerationReportsFilters(viewModel: viewModel)
        XCTAssertNoThrow(try filters.inspect().find(text: "Severity"))
        XCTAssertNoThrow(try filters.inspect().find(text: "Most reports"))
    }

    func testGroupedFixtureRendersMixedBanEvasionAndDuplicateWaveActions() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.clustered
        )
        let viewModel = ModerationReportsViewModel(client: nil, viewerTier: .administrator)
        let mixed = try XCTUnwrap(response.clusters.first {
            $0.entityType == "user" && $0.entityId == "00000000-0000-7000-8000-000000000201"
        })
        let duplicate = try XCTUnwrap(response.duplicateClusters.first)

        let clusterCard = StaffModerationReportClusterCard(
            cluster: mixed, viewModel: viewModel, onNavigate: { _ in }, onConfirm: { _ in }
        )
        XCTAssertNoThrow(try clusterCard.inspect().find(button: "Confirm ban evasion"))
        XCTAssertNoThrow(try clusterCard.inspect().find(button: "Dismiss loaded reports"))

        let duplicateCard = ModerationReportDuplicateClusterCard(
            duplicateCluster: duplicate, viewModel: viewModel, onRemove: {}
        )
        XCTAssertNoThrow(try duplicateCard.inspect().find(text: "3 posts · 3 reports"))
        XCTAssertNoThrow(try duplicateCard.inspect().find(button: "Remove all loaded posts"))
    }

    func testRefreshFailureKeepsRowsAndActionErrorsVisibleAsNonblockingFeedback() throws {
        let fixture = try ModerationReportsTestFixtures.nestedDuplicate(
            reportIds: ["failed", "skipped"], systemIds: ["skipped"]
        )
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: fixture
        )
        let viewModel = ModerationReportsViewModel(client: nil, viewerTier: .administrator)
        viewModel.duplicateClusters = response.duplicateClusters
        viewModel.selectedReportIds = ["failed", "skipped"]
        viewModel.paginationError = .verbatim("The refreshed queue could not be loaded.")
        viewModel.actionErrors = [
            "failed": .verbatim("The action could not be completed."),
            "skipped": .verbatim("Only ordinary post and comment reports can be removed.")
        ]
        let sut = ModerationReportsSurface(
            viewModel: viewModel,
            isSignedIn: true,
            onNavigate: { _ in },
            showSignIn: {}
        )
        let inspection = try sut.inspect()

        XCTAssertNoThrow(try inspection.find(text: "2 posts · 2 reports"))
        XCTAssertNoThrow(try inspection.find(text: "The refreshed queue could not be loaded."))
        XCTAssertNoThrow(try inspection.find(text: "The action could not be completed."))
        XCTAssertNoThrow(try inspection.find(text: "Only ordinary post and comment reports can be removed."))
        XCTAssertNoThrow(try inspection.find(button: "Remove selected"))
        XCTAssertThrowsError(try inspection.find(button: "Try again"))
    }

    func testInitialEmptyLoadingAndErrorStatesRender() throws {
        let empty = ModerationReportsViewModel(client: nil, viewerTier: .siteModerator)
        let emptyInspection = try surface(empty).inspect()
        XCTAssertNoThrow(try emptyInspection.find(text: "No reports"))
        XCTAssertNoThrow(try emptyInspection.find(text: "No reports match these filters."))

        let loading = ModerationReportsViewModel(client: nil, viewerTier: .siteModerator)
        loading.isLoading = true
        XCTAssertNoThrow(try surface(loading).inspect().find(text: "Loading reports"))

        let failed = ModerationReportsViewModel(client: nil, viewerTier: .siteModerator)
        failed.loadError = .verbatim("The report queue could not be loaded.")
        let failedInspection = try surface(failed).inspect()
        XCTAssertNoThrow(try failedInspection.find(text: "The report queue could not be loaded."))
        XCTAssertNoThrow(try failedInspection.find(button: "Try again"))
    }

    func testBulkRemoveStagesConfirmationBeforeMutation() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffFlatModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.staffFlat
        )
        let report = try XCTUnwrap(response.reports.first)
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        viewModel.mode = .flat
        viewModel.staffReports = [report]
        viewModel.selectedReportIds = [report.id]
        let sut = surface(viewModel)
        try sut.inspect().find(button: "Remove selected").tap()

        XCTAssertEqual(viewModel.pendingConfirmation?.buttonTitle, "Remove content")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testFailedDuplicateRefreshHidesRemovedWaveAndKeepsFailedSkippedWave() async throws {
        let removedResponse = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["removed"], duplicateId: "removed-wave", signal: "content_hash_duplicate"
            )
        )
        let retainedResponse = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ModerationReportsTestFixtures.nestedDuplicate(
                reportIds: ["failed", "skipped"], duplicateId: "retained-wave",
                signal: "embeddings_similarity", systemIds: ["skipped"]
            )
        )
        let viewModel = try ModerationReportsViewModel(client: makeClient(), viewerTier: .administrator)
        viewModel.duplicateClusters = removedResponse.duplicateClusters + retainedResponse.duplicateClusters
        viewModel.selectedReportIds = ["removed", "failed", "skipped"]
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-removed"] = (Data(), 204)
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-failed"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.errors["/api/v1/reports"] = URLError(.timedOut)

        await viewModel.bulkRemoveSelected()
        let inspection = try surface(viewModel).inspect()

        XCTAssertThrowsError(try inspection.find(text: "Duplicate-content wave"))
        XCTAssertThrowsError(try inspection.find(text: "1 posts · 1 reports"))
        XCTAssertNoThrow(try inspection.find(text: "Similar-content wave"))
        XCTAssertNoThrow(try inspection.find(text: "2 posts · 2 reports"))
        XCTAssertNoThrow(try inspection.find(button: "Remove all loaded posts"))
        XCTAssertEqual(viewModel.selectedReportIds, ["failed", "skipped"])
        XCTAssertNotNil(viewModel.paginationError)
    }

    private func surface(_ viewModel: ModerationReportsViewModel) -> ModerationReportsSurface {
        ModerationReportsSurface(
            viewModel: viewModel,
            isSignedIn: true,
            onNavigate: { _ in },
            showSignIn: {}
        )
    }
}

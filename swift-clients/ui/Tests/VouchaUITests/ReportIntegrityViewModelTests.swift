@testable import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ReportIntegrityViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testAllFilterUsesMixedDefaultFixtureWithoutStatusQuery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/report-integrity/flags"] = (
            ApiFixtureLoader.data("native.moderation.report-integrity.default"),
            200
        )
        let viewModel = try ReportIntegrityViewModel(
            service: APIReportIntegrityService(client: makeClient()),
            viewerTier: .administrator
        )
        viewModel.selectedStatus = .all

        await viewModel.load()

        XCTAssertEqual(viewModel.flags.map(\.resolution), [nil, .dismissed])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25")
    }

    func testAPIServiceUsesFixtureBackedListAndPenaltyEndpoints() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/report-integrity/flags"] = (
            ApiFixtureLoader.data("native.moderation.report-integrity.pending"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/report-integrity/flags/report-flag-1/penalties"] = (
            ApiFixtureLoader.data("native.moderation.report-integrity.penalty"),
            201
        )
        let viewModel = try ReportIntegrityViewModel(
            service: APIReportIntegrityService(client: makeClient()),
            viewerTier: .administrator
        )

        await viewModel.load()
        let flag = try XCTUnwrap(viewModel.flags.first)
        await viewModel.penalizeReporters(flag)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25&status=pending")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "POST"])
        XCTAssertTrue(viewModel.flags.isEmpty)
        XCTAssertEqual(viewModel.penalizedReporterCounts[flag.id], 2)
    }

    func testFiltersAndPaginatesWithOpaqueCursorAndUniqueIds() async throws {
        let service = ReportIntegrityServiceDouble()
        service.pages = try [
            .init(
                result: .success(IntegrityTestSupport.reportPage(
                    [IntegrityTestSupport.reportFlag(id: "one")],
                    hasMore: true,
                    cursor: "opaque.cursor+one"
                )),
                delay: .zero
            ),
            .init(
                result: .success(IntegrityTestSupport.reportPage([
                    IntegrityTestSupport.reportFlag(id: "one"),
                    IntegrityTestSupport.reportFlag(id: "two")
                ])),
                delay: .zero
            ),
            .init(
                result: .success(IntegrityTestSupport.reportPage([
                    IntegrityTestSupport.reportFlag(id: "resolved", resolution: "dismissed")
                ])),
                delay: .zero
            )
        ]
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)

        await viewModel.load()
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.flags.map(\.id), ["one", "two"])
        XCTAssertEqual(service.calls.count, 2)
        XCTAssertEqual(service.calls[0].0, .pending)
        XCTAssertNil(service.calls[0].1)
        XCTAssertEqual(service.calls[1].1, "opaque.cursor+one")
        XCTAssertEqual(service.calls[1].2, 25)

        await viewModel.selectStatus(.resolved)
        XCTAssertEqual(viewModel.flags.map(\.id), ["resolved"])
        XCTAssertEqual(service.calls.last?.0, .resolved)
    }

    func testAllFilterOmitsStatusAndRejectsStaleFilterResponse() async throws {
        let service = ReportIntegrityServiceDouble()
        service.pages = try [
            .init(
                result: .success(IntegrityTestSupport.reportPage([
                    IntegrityTestSupport.reportFlag(id: "stale")
                ])),
                delay: .milliseconds(80)
            ),
            .init(
                result: .success(IntegrityTestSupport.reportPage([
                    IntegrityTestSupport.reportFlag(id: "current", resolution: "dismissed")
                ])),
                delay: .zero
            ),
            .init(
                result: .success(IntegrityTestSupport.reportPage([
                    IntegrityTestSupport.reportFlag(id: "all")
                ])),
                delay: .zero
            )
        ]
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)

        let stale = Task { await viewModel.load() }
        await Task.yield()
        await viewModel.selectStatus(.resolved)
        await stale.value

        XCTAssertEqual(viewModel.flags.map(\.id), ["current"])
        await viewModel.selectStatus(.all)
        XCTAssertEqual(viewModel.flags.map(\.id), ["all"])
        XCTAssertNil(service.calls.last?.0)
    }

    func testOneContinuationAtATimeAndFailurePreservesRowsForRetry() async throws {
        let service = ReportIntegrityServiceDouble()
        service.pages = try [
            .init(
                result: .success(IntegrityTestSupport.reportPage(
                    [IntegrityTestSupport.reportFlag(id: "one")],
                    hasMore: true,
                    cursor: "next"
                )),
                delay: .zero
            ),
            .init(result: .failure(VouchaError.unexpected("Page failed.")), delay: .milliseconds(50)),
            .init(
                result: .success(IntegrityTestSupport.reportPage([
                    IntegrityTestSupport.reportFlag(id: "two")
                ])),
                delay: .zero
            )
        ]
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        await viewModel.load()

        async let first: Void = viewModel.loadMore()
        await Task.yield()
        async let duplicate: Void = viewModel.loadMore()
        _ = await (first, duplicate)

        XCTAssertEqual(service.calls.count, 2)
        XCTAssertEqual(viewModel.flags.map(\.id), ["one"])
        XCTAssertEqual(viewModel.continuationErrorMessage, "Page failed.")

        await viewModel.loadMore()
        XCTAssertEqual(viewModel.flags.map(\.id), ["one", "two"])
        XCTAssertNil(viewModel.continuationErrorMessage)
    }

    func testDismissRetriesAndUsesAuthoritativeReturnedFlag() async throws {
        let pending = try IntegrityTestSupport.reportFlag()
        let resolved = try IntegrityTestSupport.reportFlag(resolution: "dismissed")
        let service = ReportIntegrityServiceDouble()
        service.dismissResults = [
            .failure(VouchaError.unexpected("Dismiss failed.")),
            .success(resolved)
        ]
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.selectedStatus = .all
        viewModel.flags = [pending]

        await viewModel.dismiss(pending)
        XCTAssertEqual(viewModel.flags.first?.resolution, nil)
        XCTAssertEqual(viewModel.mutationErrorMessages[pending.id], "Dismiss failed.")

        await viewModel.dismiss(pending)
        XCTAssertEqual(viewModel.flags.first?.resolution, .dismissed)
        XCTAssertNil(viewModel.mutationErrorMessages[pending.id])
        XCTAssertEqual(service.dismissCalls, [pending.id, pending.id])
    }

    func testPenaltyUsesAuthoritativeFlagAndPerFlagMutualExclusion() async throws {
        let pending = try IntegrityTestSupport.reportFlag()
        let penalized = try IntegrityTestSupport.reportFlag(resolution: "penalized")
        let service = ReportIntegrityServiceDouble()
        service.penaltyDelay = .milliseconds(60)
        service.penaltyResults = try [
            .success(IntegrityTestSupport.reportPenalty(flag: penalized, count: 3))
        ]
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [pending]

        async let first: Void = viewModel.penalizeReporters(pending)
        await waitForPenaltyCall(service)
        async let duplicate: Void = viewModel.penalizeReporters(pending)
        _ = await (first, duplicate)

        XCTAssertEqual(service.penaltyCalls, [pending.id])
        XCTAssertTrue(viewModel.flags.isEmpty)
        XCTAssertEqual(viewModel.penalizedReporterCounts[pending.id], 3)
    }

    func testOnlyAdministratorsCanLoadOrMutate() async throws {
        for tier in [
            IntegrityViewerTier.anonymous,
            .member,
            .siteModerator,
            .customerSupport
        ] {
            let service = ReportIntegrityServiceDouble()
            let viewModel = ReportIntegrityViewModel(service: service, viewerTier: tier)
            let flag = try IntegrityTestSupport.reportFlag()
            viewModel.flags = [flag]

            await viewModel.load()
            await viewModel.dismiss(flag)
            await viewModel.penalizeReporters(flag)

            XCTAssertTrue(service.calls.isEmpty)
            XCTAssertTrue(service.dismissCalls.isEmpty)
            XCTAssertTrue(service.penaltyCalls.isEmpty)
        }
    }

    private func waitForPenaltyCall(_ service: ReportIntegrityServiceDouble) async {
        for _ in 0 ..< 100 where service.penaltyCalls.isEmpty {
            await Task.yield()
        }
    }
}

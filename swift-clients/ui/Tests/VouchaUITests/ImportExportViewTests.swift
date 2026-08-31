import SwiftUI
import UniformTypeIdentifiers
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportViewTests: NativeRouteSurfaceViewModelTestCase {
    func testSourceFilePickerOnlyOffersSupportedFormats() {
        XCTAssertEqual(ImportExportView.sourceImportContentTypes, [.commaSeparatedText, .xml, .vouchaOPML])
        XCTAssertFalse(ImportExportView.sourceImportContentTypes.contains(.plainText))
    }

    func testTopicRouteUsesNativeTextEditorAndShareControls() throws {
        let view = ImportExportView(route: .topics, viewModel: makeViewModel(route: .topics))
        XCTAssertNoThrow(try view.inspect().find(ViewType.TextEditor.self))
        XCTAssertEqual(try view.inspect().find(button: "Prepare export").labelView().text().string(), "Prepare export")
    }

    func testTopicValidationFailureRendersNativeAlert() async throws {
        let viewModel = makeViewModel(route: .topics)
        await viewModel.importText()
        let view = ImportExportView(route: .topics, viewModel: viewModel)

        let alert = try view.inspect().form().alert()
        XCTAssertEqual(try alert.title().string(), "Import/export")
        XCTAssertEqual(try alert.message().text().string(), "Enter at least one value.")
    }

    func testSourceValidationFailureRendersNativeAlert() async throws {
        let route = ImportExportRoute.sources(initialExportFilter: nil)
        let viewModel = makeViewModel(route: route)
        await viewModel.importText()
        let view = ImportExportView(route: route, viewModel: viewModel)

        let alert = try view.inspect().form().alert()
        XCTAssertEqual(try alert.title().string(), "Import/export")
        XCTAssertEqual(try alert.message().text().string(), "Enter at least one value.")
    }

    func testTopicPartialOutcomesAndSingleFlightControlsRender() throws {
        let viewModel = makeViewModel(route: .topics)
        viewModel.results = [
            ImportResult(input: "Travel", status: .followed),
            ImportResult(input: "Local News", status: .recommendationCreated),
            ImportResult(input: "Missing", status: .error, error: "Topic not found")
        ]
        viewModel.isWorking = true
        let view = ImportExportView(route: .topics, viewModel: viewModel)

        XCTAssertNoThrow(try view.inspect().find(text: "Followed"))
        XCTAssertNoThrow(try view.inspect().find(text: "Recommendation created"))
        XCTAssertNoThrow(try view.inspect().find(text: "Failed: Topic not found"))
        XCTAssertTrue(try view.inspect().find(button: "Import topics").isDisabled())
        XCTAssertTrue(try view.inspect().find(button: "Prepare export").isDisabled())
    }

    func testResultRowsGiveDuplicateIdlessOutcomesStableUniqueIdentities() {
        let viewModel = makeViewModel(route: .topics)
        viewModel.results = [
            ImportResult(input: "Travel", status: .followed),
            ImportResult(input: "Travel", status: .alreadyFollowing),
            ImportResult(input: "Local News", status: .recommendationCreated),
            ImportResult(id: "backend-row", input: "Source", status: .pending)
        ]

        let initialIdentities = viewModel.resultRows.map(\.id)
        XCTAssertEqual(initialIdentities, [
            .fallback(input: "Travel", occurrence: 0),
            .fallback(input: "Travel", occurrence: 1),
            .fallback(input: "Local News", occurrence: 0),
            .backend("backend-row")
        ])
        XCTAssertEqual(Set(initialIdentities).count, initialIdentities.count)

        viewModel.results = [
            ImportResult(input: "Travel", status: .error, error: "Changed"),
            ImportResult(input: "Travel", status: .followed),
            ImportResult(input: "Local News", status: .error),
            ImportResult(id: "backend-row", input: "Source", status: .followed)
        ]
        XCTAssertEqual(viewModel.resultRows.map(\.id), initialIdentities)
    }

    func testSourceRouteUsesNativeFileImporterAndProgressControls() throws {
        let route = ImportExportRoute.sources(initialExportFilter: .podcast)
        let view = ImportExportView(route: route, viewModel: makeViewModel(route: route))
        XCTAssertNoThrow(try view.inspect().find(ViewType.TextEditor.self))
        XCTAssertEqual(
            try view.inspect().find(button: "Choose CSV, OPML, or XML").labelView().text().string(),
            "Choose CSV, OPML, or XML"
        )
    }

    func testImportExportRoutesHaveDedicatedDestinations() {
        XCTAssertEqual(
            NativeRouteCatalog.matchingRoute(for: "/my/topics/import-export")?.entry.destinationIdentifier,
            .topicImportExport
        )
        for path in [
            "/my/news-sources/import-export",
            "/my/podcasts/import-export",
            "/my/channels/import-export",
            "/my/sources/import-export"
        ] {
            XCTAssertEqual(
                NativeRouteCatalog.matchingRoute(for: path)?.entry.destinationIdentifier,
                .sourceImportExport
            )
        }
    }

    func testImportExportDestinationsRequireAnAuthenticatedSession() throws {
        XCTAssertTrue(NativeRouteDestinationIdentifier.topicImportExport.requiresAuthenticatedSession)
        XCTAssertTrue(NativeRouteDestinationIdentifier.sourceImportExport.requiresAuthenticatedSession)

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/topics/import-export"))
        var didRequestSignIn = false
        let view = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isSignedIn: false,
            showSignIn: { didRequestSignIn = true }
        )

        XCTAssertNoThrow(try view.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try view.inspect().find(ViewType.TextEditor.self))
        try view.inspect().find(button: "Sign in").tap()
        XCTAssertTrue(didRequestSignIn)
    }

    func testNativeRouteRootRendersImportExportViewInsteadOfFallback() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/topics/import-export"))
        let view = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        XCTAssertNoThrow(try view.inspect().find(ViewType.TextEditor.self))
        XCTAssertNoThrow(try view.inspect().find(button: "Prepare export"))
        XCTAssertThrowsError(try view.inspect().find(text: "No native destination"))
    }

    func testSourceNativeRouteRootRendersImportExportViewInsteadOfFallback() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/sources/import-export"))
        let view = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        XCTAssertNoThrow(try view.inspect().find(ViewType.TextEditor.self))
        XCTAssertNoThrow(try view.inspect().find(button: "Import URLs"))
        XCTAssertNoThrow(try view.inspect().find(button: "Choose CSV, OPML, or XML"))
        XCTAssertThrowsError(try view.inspect().find(text: "No native destination"))
    }

    func testPreparedExportShowsNativeShareControl() throws {
        let viewModel = makeViewModel(route: .topics)
        viewModel.exportURL = URL(fileURLWithPath: "/tmp/topics.json")
        let view = ImportExportView(route: .topics, viewModel: viewModel)

        XCTAssertEqual(try view.inspect().find(text: "Share export").string(), "Share export")
    }

    func testSourceProgressShowsRetryStopAndStatusRetryControls() throws {
        let route = ImportExportRoute.sources(initialExportFilter: nil)
        let viewModel = makeViewModel(route: route)
        viewModel.sourceSummary = summary(pending: 1)
        viewModel.batchId = "70000000-0000-7000-8000-000000000001"
        viewModel.results = [
            ImportResult(input: "https://example.test/feed.xml", status: .pending, error: "Temporary failure")
        ]
        viewModel.isMonitoring = true

        var view = ImportExportView(route: route, viewModel: viewModel)
        try view.inspect().find(button: "Stop monitoring").tap()
        XCTAssertFalse(viewModel.isMonitoring)
        XCTAssertEqual(
            try view.inspect().find(text: "Retrying: Temporary failure").string(),
            "Retrying: Temporary failure"
        )

        viewModel.isMonitoring = false
        viewModel.errorMessage = "Status failed"
        view = ImportExportView(route: route, viewModel: viewModel)
        XCTAssertNoThrow(try view.inspect().find(button: "Try status again"))
    }

    func testStatusRetryButtonRunsSameBatchAndRendersPartialOutcome() async throws {
        let recorder = RenderedStatusRecorder()
        let route = ImportExportRoute.sources(initialExportFilter: nil)
        let viewModel = ImportExportViewModel(route: route, service: recorder.service(), delay: {})
        viewModel.sourceSummary = summary(pending: 1)
        viewModel.batchId = RenderedStatusRecorder.batchId
        viewModel.errorMessage = "Status failed"
        let view = ImportExportView(route: route, viewModel: viewModel)

        try view.inspect().find(button: "Try status again").tap()
        await recorder.waitForStatusRequest()
        XCTAssertTrue(viewModel.isMonitoring)
        let monitorTask = try XCTUnwrap(viewModel.monitorTask)
        await recorder.completeStatusRequest()
        await monitorTask.value

        XCTAssertEqual(viewModel.results.map(\.status), [.followed, .error])
        let updated = ImportExportView(route: route, viewModel: viewModel)
        XCTAssertNoThrow(try updated.inspect().find(text: "Followed"))
        XCTAssertNoThrow(try updated.inspect().find(text: "Failed: No feed"))
    }

    private func summary(pending: Int) -> RssFeedImportSummary {
        RssFeedImportSummary(
            id: "70000000-0000-7000-8000-000000000001",
            totalRows: 1,
            completedRows: 0,
            failedRows: 0,
            pendingRows: pending,
            completedAt: nil,
            createdAt: Date(timeIntervalSince1970: 0)
        )
    }

    private func makeViewModel(route: ImportExportRoute) -> ImportExportViewModel {
        ImportExportViewModel(
            route: route,
            service: ImportExportService(
                importTopics: { _ in TopicImportResponse(results: []) },
                importSources: { _ in throw URLError(.badServerResponse) },
                sourceStatus: { _ in throw URLError(.badServerResponse) },
                downloadExport: { _, _, _ in throw URLError(.badServerResponse) }
            )
        )
    }
}

private actor RenderedStatusRecorder {
    static let batchId = "70000000-0000-7000-8000-000000000001"
    private var statusWaiters: [CheckedContinuation<Void, Never>] = []
    private var responseContinuation: CheckedContinuation<Void, Never>?
    private var didStart = false

    nonisolated func service() -> ImportExportService {
        ImportExportService(
            importTopics: { _ in TopicImportResponse(results: []) },
            importSources: { _ in throw URLError(.badServerResponse) },
            sourceStatus: { _ in await self.status() },
            downloadExport: { _, _, _ in throw URLError(.badServerResponse) }
        )
    }

    func waitForStatusRequest() async {
        guard !didStart else { return }
        await withCheckedContinuation { statusWaiters.append($0) }
    }

    func completeStatusRequest() {
        responseContinuation?.resume()
        responseContinuation = nil
    }

    private func status() async -> RssFeedImportStatus {
        didStart = true
        let waiters = statusWaiters
        statusWaiters = []
        waiters.forEach { $0.resume() }
        await withCheckedContinuation { responseContinuation = $0 }
        return RssFeedImportStatus(import: summary(), rows: [
            ImportResult(input: "https://example.test/feed.xml", status: .followed),
            ImportResult(input: "https://invalid.example.test/feed.xml", status: .error, error: "No feed")
        ])
    }

    private func summary() -> RssFeedImportSummary {
        RssFeedImportSummary(
            id: Self.batchId,
            totalRows: 2,
            completedRows: 1,
            failedRows: 1,
            pendingRows: 0,
            completedAt: Date(timeIntervalSince1970: 1),
            createdAt: Date(timeIntervalSince1970: 0)
        )
    }
}

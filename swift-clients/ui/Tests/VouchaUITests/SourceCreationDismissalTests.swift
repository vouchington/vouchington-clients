import Foundation
import Observation
import SwiftUI
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SourceCreationDismissalTests: NativeRouteSurfaceViewModelTestCase {
    private let listPath = "/api/v1/users/account/rss-feeds/following"

    private func registerCreation(pending: Bool = false, outcome: ImportResultStatus = .sourceCreated) throws {
        let summary = RssFeedImportSummary(
            id: "batch",
            totalRows: 1,
            completedRows: pending || outcome == .skipped ? 0 : 1,
            skippedRows: outcome == .skipped ? 1 : 0,
            failedRows: 0,
            pendingRows: pending ? 1 : 0,
            completedAt: nil,
            createdAt: Date()
        )
        CannedFeedURLProtocol.requestHandlers["POST /api/v1/rss-feeds"] = try (
            APIClient.makeEncoder().encode(RssFeedImportSubmission(
                import: summary,
                statusUrl: "/api/v1/my/import/rss-feeds/batch"
            )),
            202
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/import/rss-feeds/batch"] = try (
            APIClient.makeEncoder().encode(RssFeedImportStatus(import: summary, rows: [
                .init(input: "feed", status: outcome, entityId: "feed", topicSlug: "created-source")
            ])), 200
        )
    }

    private var page: Data {
        Data(
            #"{"results":[{"id":"feed","title":"Created source","feed_type":"article","rss_feed_url":{"url":"https://example.com/feed"},"etag":null,"home_page_url":null,"last_modified_at":null,"hostname":null,"publisher_type":null,"podcast_show":null}],"topic_elections":{}}"#
                .utf8
        )
    }

    func testRenderedSuccessfulDismissalReloadsVisibleListAndInvalidatesOtherScope() async throws {
        try registerCreation()
        let list = try SourcesListViewModel(client: makeClient(), userId: "account", feedType: "article")
        list.yourLoaded = true
        list.allLoaded = true
        let view = SourcesListView(viewModel: list, scope: .your, navigationTitle: "Sources")
        var presented = true
        let sheet = view.creationSheet(isPresented: Binding(get: { presented }, set: { presented = $0 }))
        sheet.model.url = "https://example.com/feed"
        await sheet.model.create()
        CannedFeedURLProtocol.handlers[listPath] = (page, 200)
        let reloaded = expectation(description: "dismissal publishes created source")
        observeItems(list, completion: reloaded)
        try cancelUsingRenderedControl(sheet)
        XCTAssertFalse(presented)
        try sheet.inspect().form().callOnDisappear()
        await fulfillment(of: [reloaded], timeout: 2)
        XCTAssertNoThrow(try view.inspect().find(text: "Created source"))
        XCTAssertFalse(list.allLoaded)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(listPath), 1)
        try sheet.inspect().form().callOnDisappear()
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(listPath), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count, 1)
    }

    private func cancelUsingRenderedControl(_ sheet: SourceCreationView) throws {
        let button = try sheet.inspect().find(button: "Cancel")
        XCTAssertFalse(button.isDisabled())
        XCTAssertEqual(try button.role(), .cancel)
        let content = try XCTUnwrap(Mirror(reflecting: button).children.first { $0.label == "content" }?.value)
        let shortcut = try XCTUnwrap(keyboardShortcut(in: content))
        XCTAssertEqual(shortcut.key, KeyEquivalent.escape)
        XCTAssertEqual(shortcut.modifiers, [])
        // Escape dispatches this button action through SwiftUI's cancelAction shortcut.
        try button.tap()
    }

    private func keyboardShortcut(in value: Any, depth: Int = 0) -> KeyboardShortcut? {
        if let shortcut = value as? KeyboardShortcut { return shortcut }
        guard depth < 12 else { return nil }
        for child in Mirror(reflecting: value).children {
            if let shortcut = keyboardShortcut(in: child.value, depth: depth + 1) { return shortcut }
        }
        return nil
    }

    func testRenderedCancelClosesUnsubmittedAndTerminalFailureSheets() async throws {
        for submitted in [false, true] {
            CannedFeedURLProtocol.reset()
            try registerCreation(outcome: .error)
            let list = try SourcesListViewModel(client: makeClient(), userId: "account", feedType: "article")
            list.yourLoaded = true
            list.allLoaded = true
            let view = SourcesListView(viewModel: list, scope: .your, navigationTitle: "Sources")
            var presented = true
            let sheet = view.creationSheet(isPresented: Binding(get: { presented }, set: { presented = $0 }))
            if submitted {
                sheet.model.url = "https://example.com/feed"
                await sheet.model.create()
                XCTAssertTrue(sheet.model.isTerminal)
            }
            try cancelUsingRenderedControl(sheet)
            XCTAssertFalse(presented)
            try sheet.inspect().form().callOnDisappear()
            XCTAssertFalse(sheet.model.isWorking)
            XCTAssertTrue(list.yourLoaded)
            XCTAssertTrue(list.allLoaded)
            XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(listPath), 0)
            XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count, submitted ? 1 : 0)
        }
    }

    func testRenderedCancelClosesAndFencesActivePollWithoutRefresh() async throws {
        try registerCreation(pending: true)
        let list = try SourcesListViewModel(client: makeClient(), userId: "account", feedType: "article")
        list.yourLoaded = true
        list.allLoaded = true
        let view = SourcesListView(viewModel: list, scope: .your, navigationTitle: "Sources")
        var presented = true
        let sheet = view.creationSheet(isPresented: Binding(get: { presented }, set: { presented = $0 }))
        let statusPath = "/api/v1/my/import/rss-feeds/batch"
        CannedFeedURLProtocol.suspendResponse(path: statusPath)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: statusPath, method: "GET")
        sheet.model.url = "https://example.com/feed"
        let operation = Task { await sheet.model.create() }
        _ = try await barrier.wait()
        XCTAssertTrue(sheet.model.isWorking)
        try cancelUsingRenderedControl(sheet)
        XCTAssertFalse(presented)
        XCTAssertFalse(sheet.model.isWorking)
        try sheet.inspect().form().callOnDisappear()
        CannedFeedURLProtocol.releaseOldestResponse(path: statusPath)
        await operation.value
        XCTAssertFalse(sheet.model.isWorking)
        XCTAssertNil(sheet.model.currentDestination)
        XCTAssertTrue(list.yourLoaded)
        XCTAssertTrue(list.allLoaded)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(listPath), 0)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count, 1)
    }

    private func observeItems(_ list: SourcesListViewModel, completion: XCTestExpectation) {
        withObservationTracking { _ = list.items } onChange: {
            Task { @MainActor in
                if list.items.isEmpty { self.observeItems(list, completion: completion) }
                else { completion.fulfill() }
            }
        }
    }

    func testRenderedDismissalDiscardsReloadAfterAccountOrAPIChanges() async throws {
        for replacement in ["other-account|api-one", "account|api-two"] {
            CannedFeedURLProtocol.reset()
            try registerCreation()
            var identity = "account|api-one"
            var waitingForResponseGuard = false
            let guarded = expectation(description: "completed reload checks its captured owner")
            let list = try SourcesListViewModel(client: makeClient(), userId: "account", feedType: "article")
            let view = SourcesListView(
                viewModel: list,
                scope: .your,
                navigationTitle: "Sources",
                creationIdentity: {
                    if waitingForResponseGuard {
                        waitingForResponseGuard = false
                        guarded.fulfill()
                    }
                    return identity
                }
            )
            let sheet = view.creationSheet
            sheet.model.url = "https://example.com/feed"
            await sheet.model.create()
            CannedFeedURLProtocol.handlers[listPath] = (page, 200)
            CannedFeedURLProtocol.suspendResponse(path: listPath)
            let barrier = CannedFeedURLProtocol.requestBarrier(path: listPath, method: "GET")
            try sheet.inspect().form().callOnDisappear()
            _ = try await barrier.wait()
            identity = replacement
            waitingForResponseGuard = true
            CannedFeedURLProtocol.releaseOldestResponse(path: listPath)
            await fulfillment(of: [guarded], timeout: 2)
            XCTAssertTrue(list.yourSources.isEmpty)
            XCTAssertThrowsError(try view.inspect().find(text: "Created source"))
        }
    }

    func testRenderedSkippedImportIsFailureWithoutNavigationOrListRefresh() async throws {
        try registerCreation(outcome: .skipped)
        let list = try SourcesListViewModel(client: makeClient(), userId: "account", feedType: "article")
        list.yourLoaded = true
        list.allLoaded = true
        let view = SourcesListView(viewModel: list, scope: .your, navigationTitle: "Sources")
        let sheet = view.creationSheet
        sheet.model.url = "https://example.com/feed"
        await sheet.model.create()
        XCTAssertTrue(sheet.model.isTerminal)
        XCTAssertNil(sheet.model.currentDestination)
        XCTAssertNoThrow(try sheet.inspect().find(ViewType.Text.self, where: { try $0.string().hasPrefix("Failed:") }))
        XCTAssertThrowsError(try sheet.inspect().find(button: "Open source"))
        try sheet.inspect().form().callOnDisappear()
        XCTAssertTrue(list.yourLoaded)
        XCTAssertTrue(list.allLoaded)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(listPath), 0)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/rss-feeds/feed"), 0)
    }

    func testRenderedCancellationAndStaleOwnerDoNotRefresh() async throws {
        for completed in [false, true] {
            CannedFeedURLProtocol.reset()
            try registerCreation(pending: !completed)
            if !completed {
                CannedFeedURLProtocol.handlers["/api/v1/my/import/rss-feeds/batch"] = (Data("{}".utf8), 503)
            }
            var identity = "account|api-one"
            let list = try SourcesListViewModel(client: makeClient(), userId: "account", feedType: "article")
            list.yourLoaded = true
            list.allLoaded = true
            let view = SourcesListView(
                viewModel: list,
                scope: .your,
                navigationTitle: "Sources",
                creationIdentity: { identity }
            )
            let sheet = view.creationSheet
            sheet.model.url = "https://example.com/feed"
            await sheet.model.create()
            if completed { identity = "other-account|api-two" }
            else { XCTAssertFalse(sheet.model.isTerminal) }
            try sheet.inspect().form().callOnDisappear()
            XCTAssertTrue(list.yourLoaded)
            XCTAssertTrue(list.allLoaded)
            XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(listPath), 0)
        }
    }
}

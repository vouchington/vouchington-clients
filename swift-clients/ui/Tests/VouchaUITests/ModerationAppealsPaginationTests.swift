import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class ModerationAppealsPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testLoadsFiltersAndPaginatesWithoutDuplicates() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.staffList([Support.appeal(id: "appeal-1")], hasNextPage: true, endCursor: "cursor-1"), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.staffList([Support.appeal(id: "appeal-1"), Support.appeal(id: "appeal-2")]), 200
        )
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.summaryPagination.items.map(\.id), ["appeal-1", "appeal-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs[0].query, "limit=25&status=pending")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs[1].query, "limit=25&status=pending&after=cursor-1")
        XCTAssertFalse(viewModel.hasMore)

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.staffList([Support.appeal(id: "appeal-3", status: "dismissed")]), 200
        )
        await viewModel.selectStatus(.dismissed)
        XCTAssertEqual(viewModel.summaryPagination.items.map(\.id), ["appeal-3"])
    }

    func testIgnoresStaleFilterResponseAndPreservesRowsWhenLoadMoreFails() async throws {
        let path = "/api/v1/appeals"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Support.staffList([Support.appeal(id: "stale")]), 200, 0),
            (Support.staffList([Support.appeal(id: "resolved", status: "resolved")], hasNextPage: true), 200, 0)
        ]
        let viewModel = try staffViewModel()

        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let staleLoad = Task { await viewModel.load() }
        guard await Support.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await staleLoad.value
            return
        }

        let resolvedLoad = Task { await viewModel.selectStatus(.resolved) }
        guard await Support.waitForCapturedCannedFeedRequests(path: path, minimumCount: 2) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await resolvedLoad.value
            await staleLoad.value
            return
        }
        CannedFeedURLProtocol.releaseResponse(path: path)

        await resolvedLoad.value
        await staleLoad.value

        XCTAssertEqual(viewModel.summaryPagination.items.map(\.id), ["resolved"])
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"failed"}"#.utf8), 500)
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.summaryPagination.items.map(\.id), ["resolved"])
        XCTAssertNotNil(viewModel.loadMoreErrorMessage)
    }

    func testReloadClearsStaleLoadMoreState() async throws {
        let path = "/api/v1/appeals"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (Support.staffList([Support.appeal()], hasNextPage: true, endCursor: "next"), 200, 0),
            (Support.staffList([Support.appeal(id: "stale-page")]), 200, 0),
            (Support.staffList([Support.appeal(id: "fresh")]), 200, 0)
        ]
        let viewModel = try staffViewModel()
        await viewModel.load()

        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let staleLoadMore = Task { await viewModel.loadMore() }
        guard await Support.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await staleLoadMore.value
            return
        }

        let reload = Task { await viewModel.load() }
        guard await Support.waitForCapturedCannedFeedRequests(path: path, minimumCount: 3) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await reload.value
            await staleLoadMore.value
            return
        }
        CannedFeedURLProtocol.releaseResponse(path: path)

        await reload.value
        await staleLoadMore.value

        XCTAssertEqual(viewModel.summaryPagination.items.map(\.id), ["fresh"])
        XCTAssertFalse(viewModel.isLoadingMore)
    }

    private typealias Support = ModerationAppealsTestSupport

    private func staffViewModel(
        rerunPollAttempts: Int = 20,
        rerunPollDelay: @escaping @Sendable () async throws -> Void = {}
    ) throws -> ModerationAppealsViewModel {
        try ModerationAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false,
            rerunPollAttempts: rerunPollAttempts,
            rerunPollDelay: rerunPollDelay
        )
    }
}

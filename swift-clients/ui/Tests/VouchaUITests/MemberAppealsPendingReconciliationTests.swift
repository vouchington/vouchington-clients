import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class MemberAppealsPendingReconciliationTests: NativeRouteSurfaceViewModelTestCase {
    func testInitialLoadExhaustsPendingAppealsBeforeEnablingNotices() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (appealPage([], cursor: "pending-next", hasMore: true), 200, 0),
            (appealPage([ModerationAppealsTestSupport.appeal()], cursor: nil, hasMore: false), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            warningPage(id: "warning-1", cursor: nil, hasMore: false),
            200
        )
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )

        await viewModel.load()

        let target = try XCTUnwrap(viewModel.warningPagination.items.first?.appealTarget)
        XCTAssertFalse(viewModel.canAppeal(target))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals"), 2)
    }

    func testInitialPendingFailurePreservesNoticeButDisablesAppeal() async throws {
        CannedFeedURLProtocol.errors["/api/v1/appeals"] = URLError(.notConnectedToInternet)
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            warningPage(id: "warning-1", cursor: nil, hasMore: false),
            200
        )
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )

        await viewModel.load()

        let target = try XCTUnwrap(viewModel.warningPagination.items.first?.appealTarget)
        XCTAssertFalse(viewModel.canAppeal(target))
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testNoticePaginationReconcilesEveryPendingAppealBeforeEnablingOlderTargets() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (appealPage([], cursor: "pending-next", hasMore: true), 200, 0),
            (appealPage([ModerationAppealsTestSupport.appeal(id: "appeal-2")], cursor: nil, hasMore: false), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/warnings"] = [
            (warningPage(id: "warning-new", cursor: "warning-next", hasMore: true), 200, 0),
            (warningPage(id: "warning-1", cursor: nil, hasMore: false), 200, 0)
        ]
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
        await viewModel.load()

        await viewModel.loadMoreNotices(.warnings)

        XCTAssertEqual(viewModel.warningPagination.items.map(\.id), ["warning-new", "warning-1"])
        XCTAssertFalse(viewModel.canAppeal(viewModel.warningPagination.items[1].appealTarget))
        let queries = CannedFeedURLProtocol.capturedURLs.compactMap(\.query)
        XCTAssertTrue(queries.contains("limit=25&status=pending&mine=true"))
        XCTAssertTrue(queries.contains("limit=25"))
        XCTAssertEqual(
            Array(queries.suffix(2)),
            [
                "limit=25&status=pending&after=pending-next&mine=true",
                "limit=25&after=warning-next"
            ]
        )
    }

    func testNoticePaginationFailsClosedWhilePendingAppealPageIsInFlight() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            appealPage([], cursor: nil, hasMore: false),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            warningPage(id: "warning-2", cursor: nil, hasMore: false),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/appeals")
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
        viewModel.pendingAppealPagination.restoreContinuation(
            endCursor: "pending-next",
            hasMore: true
        )
        viewModel.warningPagination.restoreContinuation(endCursor: "warning-next", hasMore: true)

        let pendingLoad = Task { await viewModel.loadMoreAppeals(status: .pending) }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: "/api/v1/appeals"
        )
        XCTAssertTrue(didSuspend)

        await viewModel.loadMoreNotices(.warnings)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/my/warnings"), 0)

        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/appeals")
        await pendingLoad.value
        await viewModel.loadMoreNotices(.warnings)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/my/warnings"), 1)
    }

    private func appealPage(_ appeals: [String], cursor: String?, hasMore: Bool) -> Data {
        let endCursor = cursor.map { "\"\($0)\"" } ?? "null"
        return Data("""
        {"appeals":[\(appeals.joined(separator: ","))],
         "page_info":{"has_next_page":\(hasMore),"start_cursor":null,"end_cursor":\(endCursor)}}
        """.utf8)
    }

    private func warningPage(id: String, cursor: String?, hasMore: Bool) -> Data {
        let endCursor = cursor.map { "\"\($0)\"" } ?? "null"
        return Data("""
        {"warnings":[{"id":"\(id)","case_id":null,"user_id":"user-1",
         "community_id":null,"community_slug":null,"public_message":"Warning",
         "revoked_at":null,"created_at":"2026-07-01T12:00:00Z"}],
         "page_info":{"has_next_page":\(hasMore),"start_cursor":null,"end_cursor":\(endCursor)}}
        """.utf8)
    }
}

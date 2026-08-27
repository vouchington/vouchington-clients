import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class MemberAppealsConcurrencyTests: NativeRouteSurfaceViewModelTestCase {
    func testRetryPreservesSuccessfulNoticeStreamAndRetriesOnlyFailure() async throws {
        let viewModel = try makeViewModel()
        CannedFeedURLProtocol.errors["/api/v1/appeals"] = URLError(.notConnectedToInternet)
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (warningPage(id: "warning-1"), 200)

        await viewModel.load()

        XCTAssertEqual(viewModel.warningPagination.items.map(\.id), ["warning-1"])
        XCTAssertEqual(viewModel.failedInitialStreams, [.pendingAppeals])
        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (ModerationAppealsTestSupport.list([]), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (warningPage(id: "stale-retry"), 200)

        await viewModel.load()

        XCTAssertEqual(viewModel.warningPagination.items.map(\.id), ["warning-1"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/my/warnings"), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals"), 2)
        XCTAssertTrue(viewModel.failedInitialStreams.isEmpty)
        XCTAssertNil(viewModel.errorMessage)
    }

    func testCancellingFormKeepsSubmissionGateClosedAndIgnoresStaleSuccess() async throws {
        let viewModel = try makeViewModel()
        viewModel.pendingAppealPagination.reset(items: [])
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)
        let target = warningTarget
        viewModel.beginAppeal(target)
        viewModel.setReason(.incorrectFacts)
        viewModel.setDetails("The notice identifies the wrong event.")
        viewModel.turnstileToken = "token"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (submissionResponse(), 200)
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/appeals")

        let submission = Task { await viewModel.submitAppeal() }
        let suspended = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: "/api/v1/appeals"
        )
        XCTAssertTrue(suspended)
        viewModel.cancelAppeal()

        XCTAssertTrue(viewModel.isSubmitting)
        XCTAssertFalse(viewModel.canAppeal(target))
        viewModel.beginAppeal(target)
        XCTAssertNil(viewModel.activeTarget)

        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/appeals")
        await submission.value

        XCTAssertFalse(viewModel.isSubmitting)
        XCTAssertTrue(viewModel.appeals.isEmpty)
        XCTAssertEqual(viewModel.submissionState, .idle)
        XCTAssertTrue(viewModel.canAppeal(target))
        XCTAssertEqual(
            viewModel.draftStore.draft(for: target, currentUserId: "user-1").details,
            "The notice identifies the wrong event."
        )
    }

    private func makeViewModel() throws -> MemberAppealsViewModel {
        try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
    }

    private var warningTarget: MemberAppealTarget {
        .warning(id: "warning-1", message: nil, community: nil, createdAt: .now)
    }

    private func warningPage(id: String) -> Data {
        Data("""
        {"warnings":[{"id":"\(id)","case_id":null,"user_id":"user-1",\
        "community_id":null,"community_slug":null,"public_message":"Warning",\
        "revoked_at":null,"created_at":"2026-07-01T12:00:00Z"}],\
        "page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
        """.utf8)
    }

    private func submissionResponse() -> Data {
        Data("""
        {"appeal":\(ModerationAppealsTestSupport.appeal()),"is_duplicate":false}
        """.utf8)
    }
}

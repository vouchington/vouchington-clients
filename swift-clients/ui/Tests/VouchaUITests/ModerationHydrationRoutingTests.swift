import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class ModerationHydrationRoutingTests: NativeRouteSurfaceViewModelTestCase {
    func testAppealInitialAndNextPagesHydrateTheirSummaryDetails() async throws {
        let summaries = (0 ..< 6).map { ModerationAppealsTestSupport.appeal(id: "appeal-\($0)") }
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (
                ModerationAppealsTestSupport
                    .staffList(Array(summaries.prefix(3)), hasNextPage: true, endCursor: "next"),
                200,
                0
            ),
            (ModerationAppealsTestSupport.staffList(Array(summaries.suffix(3))), 200, 0)
        ]
        let viewModel = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.appeals.map(\.id), (0 ..< 3).map { "appeal-\($0)" })
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.appeals.map(\.id), (0 ..< 6).map { "appeal-\($0)" })
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.loadMoreErrorMessage)
        XCTAssertFalse(viewModel.hasMore)
        for index in 0 ..< 6 {
            XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals/appeal-\(index)"), 1)
        }
    }

    func testDisputeInitialAndNextPagesHydrateTheirSummaryDetails() async throws {
        let summaries = (0 ..< 6).map { ReviewDisputesTestSupport.dispute(id: "dispute-\($0)") }
        CannedFeedURLProtocol.queuedHandlers["/api/v1/disputes"] = [
            (
                ReviewDisputesTestSupport.staffList(Array(summaries.prefix(3)), hasNextPage: true, endCursor: "next"),
                200,
                0
            ),
            (ReviewDisputesTestSupport.staffList(Array(summaries.suffix(3))), 200, 0)
        ]
        let viewModel = try ReviewDisputesViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.disputes.map(\.id), (0 ..< 3).map { "dispute-\($0)" })
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.disputes.map(\.id), (0 ..< 6).map { "dispute-\($0)" })
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.loadMoreErrorMessage)
        XCTAssertFalse(viewModel.disputePagination.hasMore)
        for index in 0 ..< 6 {
            XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/disputes/dispute-\(index)"), 1)
        }
    }
}

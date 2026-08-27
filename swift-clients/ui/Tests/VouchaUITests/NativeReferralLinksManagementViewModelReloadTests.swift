@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeReferralLinksManagementViewModelReloadTests: NativeRouteSurfaceViewModelTestCase {
    func testCombinedLoadFailureRollsBackPartiallyLoadedLinks() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/referral-links"] = [
            (referralLinksPage(ids: ["old-link"], endCursor: nil, hasNextPage: false), 200, 0),
            (referralLinksPage(ids: ["new-link"], endCursor: "new-cursor", hasNextPage: true), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/referral-clicks"] = [
            (emptyReferralClicksFeed(), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.load()
        await viewModel.load()

        XCTAssertEqual(viewModel.links.map(\.id), ["old-link"])
        XCTAssertFalse(viewModel.hasMoreLinks)
        XCTAssertTrue(viewModel.state.isReferralManagementError)
    }

    func testLoadMoreLinksIsIgnoredDuringCombinedLoad() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/referral-links"] = [
            (referralLinksPage(ids: ["old-link"], endCursor: "old-cursor", hasNextPage: true), 200, 0),
            (referralLinksPage(ids: ["new-link"], endCursor: "new-cursor", hasNextPage: true), 200, 0),
            (referralLinksPage(ids: ["extra-link"], endCursor: nil, hasNextPage: false), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/referral-clicks"] = [
            (emptyReferralClicksFeed(), 200, 0),
            (emptyReferralClicksFeed(), 200, 0.1)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.load()
        async let loadingTask: Void = viewModel.load()
        try await Task.sleep(nanoseconds: 20_000_000)
        await viewModel.loadMoreLinks()
        await loadingTask

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/referral-links" }.count,
            2
        )
        XCTAssertEqual(viewModel.links.map(\.id), ["new-link"])
        XCTAssertTrue(viewModel.hasMoreLinks)
    }

    private func referralLinksPage(ids: [String], endCursor: String?, hasNextPage: Bool) -> Data {
        let results = ids.map { #"{"id":"\#($0)"}"# }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            """
            {
              "results": [\(results)],
              "page_info": {
                "has_next_page": \(hasNextPage),
                "end_cursor": \(cursor),
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func emptyReferralClicksFeed() -> Data {
        Data(
            """
            {
              "results": [],
              "clicks": {},
              "users": {},
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """.utf8
        )
    }
}

private extension LoadState {
    var isReferralManagementError: Bool {
        if case .error = self {
            return true
        }
        return false
    }
}

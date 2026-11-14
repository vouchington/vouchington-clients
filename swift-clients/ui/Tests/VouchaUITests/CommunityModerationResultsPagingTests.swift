import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityModerationResultsPagingTests: NativeRouteSurfaceViewModelTestCase {
    private let path = "/api/v1/communities/builders/posts/current-post/moderation-results"
    private let stalePath = "/api/v1/communities/builders/posts/stale-post/moderation-results"

    func testCountFollowsEveryPageAndKeepsPlatformStatus() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (ModerationResultsFixtures.page(
                status: "in_review",
                ids: (0 ..< 100).map { "a-\($0)" },
                endCursor: "cursor-1",
                hasNextPage: true
            ), 200, 0),
            (ModerationResultsFixtures.page(
                status: "approved",
                ids: ["b-0", "b-1", "b-2"],
                endCursor: "cursor-2",
                hasNextPage: false
            ), 200, 0)
        ]
        let viewModel = try makeViewModel()

        await viewModel.loadModerationResults(postId: "current-post")

        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["103 results", "Approved"])
        XCTAssertNil(viewModel.moderationResultsError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.map(\.query), [
            "limit=100",
            "limit=100&after=cursor-1"
        ])
    }

    func testNextPageWithoutCursorSurfacesErrorInsteadOfPartialCount() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (ModerationResultsFixtures.page(status: "approved", ids: ["a-0"], hasNextPage: true), 200, 0)
        ]
        let viewModel = try makeViewModel()

        await viewModel.loadModerationResults(postId: "current-post")

        XCTAssertEqual(viewModel.moderationResults, [])
        XCTAssertEqual(viewModel.moderationResultsError, UiMessage(.nativeSwiftEmptyStateUnableToLoad))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.count, 1)
    }

    func testRepeatedCursorStopsInsteadOfLooping() async throws {
        let repeating = ModerationResultsFixtures.page(
            status: "approved",
            ids: ["a-0"],
            endCursor: "same",
            hasNextPage: true
        )
        CannedFeedURLProtocol.queuedHandlers[path] = [(repeating, 200, 0), (repeating, 200, 0)]
        let viewModel = try makeViewModel()

        await viewModel.loadModerationResults(postId: "current-post")

        XCTAssertEqual(viewModel.moderationResults, [])
        XCTAssertEqual(viewModel.moderationResultsError, UiMessage(.nativeSwiftEmptyStateUnableToLoad))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.count, 2)
    }

    func testLaterPageFailureDiscardsPartialCount() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (ModerationResultsFixtures.page(
                status: "approved",
                ids: ["a-0"],
                endCursor: "cursor-1",
                hasNextPage: true
            ), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        let viewModel = try makeViewModel()

        await viewModel.loadModerationResults(postId: "current-post")

        XCTAssertEqual(viewModel.moderationResults, [])
        XCTAssertEqual(viewModel.moderationResultsError, UiMessage(.nativeSwiftEmptyStateUnableToLoad))
    }

    func testStaleLookupDoesNotFetchFurtherPagesOrOverwriteCurrentResult() async throws {
        CannedFeedURLProtocol.queuedHandlers[stalePath] = [
            (ModerationResultsFixtures.page(
                status: "rejected",
                ids: ["s-0"],
                endCursor: "stale-cursor",
                hasNextPage: true
            ), 200, 0),
            (ModerationResultsFixtures.page(status: "rejected", ids: ["s-1"]), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[path] = (
            ModerationResultsFixtures.page(status: "approved", ids: ["c-0", "c-1"]),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: stalePath)
        let viewModel = try makeViewModel()

        let staleLoad = Task { await viewModel.loadModerationResults(postId: "stale-post") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: stalePath)
        XCTAssertTrue(didSuspend)
        await viewModel.loadModerationResults(postId: "current-post")
        CannedFeedURLProtocol.releaseResponse(path: stalePath)
        await staleLoad.value

        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["2 results", "Approved"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == stalePath }.count, 1)
    }

    private func makeViewModel() throws -> CommunityDetailViewModel {
        try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)
    }
}

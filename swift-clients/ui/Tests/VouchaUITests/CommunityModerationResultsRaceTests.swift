import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityModerationResultsRaceTests: NativeRouteSurfaceViewModelTestCase {
    func testLookupPublishesFromTheLaunchingManagementTab() async throws {
        let path = "/api/v1/communities/builders/posts/current-post/moderation-results"
        CannedFeedURLProtocol.handlers[path] = (response(status: "approved", agentCount: 1), 200)
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .settings
        )

        await viewModel.loadModerationResults(postId: "current-post")

        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["1 result", "Approved"])
        XCTAssertNil(viewModel.moderationResultsError)
    }

    func testLateEarlierPostResultCannotOverwriteCurrentModerationSurface() async throws {
        let stalePath = "/api/v1/communities/builders/posts/stale-post/moderation-results"
        let currentPath = "/api/v1/communities/builders/posts/current-post/moderation-results"
        CannedFeedURLProtocol.handlers[stalePath] = (response(status: "rejected"), 200)
        CannedFeedURLProtocol.handlers[currentPath] = (response(status: "approved", agentCount: 2), 200)
        CannedFeedURLProtocol.suspendResponse(path: stalePath)
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )

        let staleLoad = Task { await viewModel.loadModerationResults(postId: "stale-post") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: stalePath)
        XCTAssertTrue(didSuspend)
        await viewModel.loadModerationResults(postId: "current-post")
        XCTAssertEqual(viewModel.moderationResults.map(\.title), ["AI agents", "Moderation summary"])
        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["2 results", "Approved"])

        CannedFeedURLProtocol.releaseResponse(path: stalePath)
        await staleLoad.value

        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["2 results", "Approved"])
    }

    func testNewLookupClearsPreviousRowsBeforeTheReplacementResponseArrives() async throws {
        let firstPath = "/api/v1/communities/builders/posts/first-post/moderation-results"
        let secondPath = "/api/v1/communities/builders/posts/second-post/moderation-results"
        CannedFeedURLProtocol.handlers[firstPath] = (response(status: "rejected"), 200)
        CannedFeedURLProtocol.handlers[secondPath] = (response(status: "approved"), 200)
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )
        await viewModel.loadModerationResults(postId: "first-post")
        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["0 results", "Rejected"])

        CannedFeedURLProtocol.suspendResponse(path: secondPath)
        let secondLoad = Task { await viewModel.loadModerationResults(postId: "second-post") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: secondPath)
        XCTAssertTrue(didSuspend)
        XCTAssertEqual(viewModel.moderationResults, [])

        CannedFeedURLProtocol.releaseResponse(path: secondPath)
        await secondLoad.value
        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["0 results", "Approved"])
    }

    func testChangingQueryPostIdClearsPreviousRows() {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderation
        )
        viewModel.moderationResults = [
            NativeRouteDestinationRow(
                id: "platform-moderation",
                icon: "checkmark.shield",
                title: .verbatim("stale title"),
                detail: .verbatim("stale detail")
            )
        ]

        viewModel.moderationQueryPostId = "other-post"

        XCTAssertEqual(viewModel.moderationResults, [])
    }

    func testChangingTabDiscardsModerationRowsAndError() {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderation
        )
        viewModel.moderationResults = [
            NativeRouteDestinationRow(
                id: "platform-moderation",
                icon: "checkmark.shield",
                title: .verbatim("stale title"),
                detail: .verbatim("stale detail")
            )
        ]
        viewModel.moderationResultsError = UiMessage(.nativeSwiftEmptyStateUnableToLoad)

        viewModel.selectedTab = .posts

        XCTAssertEqual(viewModel.moderationResults, [])
        XCTAssertNil(viewModel.moderationResultsError)
    }

    func testCommunityReloadDiscardsModerationRowsAndError() async {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderation
        )
        viewModel.moderationResults = [
            NativeRouteDestinationRow(
                id: "platform-moderation",
                icon: "checkmark.shield",
                title: .verbatim("stale title"),
                detail: .verbatim("stale detail")
            )
        ]
        viewModel.moderationResultsError = UiMessage(.nativeSwiftEmptyStateUnableToLoad)

        await viewModel.load()

        XCTAssertEqual(viewModel.moderationResults, [])
        XCTAssertNil(viewModel.moderationResultsError)
    }

    func testBlankLookupDiscardsPreviousRows() async {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderation
        )
        viewModel.moderationResults = [
            NativeRouteDestinationRow(
                id: "platform-moderation",
                icon: "checkmark.shield",
                title: .verbatim("stale title"),
                detail: .verbatim("stale detail")
            )
        ]

        await viewModel.loadModerationResults(postId: "   ")

        XCTAssertEqual(viewModel.moderationResults, [])
    }

    func testCurrentLookupFailurePublishesVisibleErrorAndSuccessfulRetryClearsIt() async throws {
        let path = "/api/v1/communities/builders/posts/current-post/moderation-results"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data("{}".utf8), 500, 0),
            (response(status: "approved"), 200, 0)
        ]
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )

        await viewModel.loadModerationResults(postId: "current-post")

        XCTAssertEqual(viewModel.moderationResults, [])
        XCTAssertEqual(viewModel.moderationResultsError, UiMessage(.nativeSwiftEmptyStateUnableToLoad))

        await viewModel.loadModerationResults(postId: "current-post")

        XCTAssertNil(viewModel.moderationResultsError)
        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["0 results", "Approved"])
    }

    private func response(status: String, agentCount: Int = 0) -> Data {
        let agents = Array(repeating: "{}", count: agentCount).joined(separator: ",")
        return Data(
            """
            {
              "community_agent_moderations": [\(agents)],
              "platform_moderation": { "status": "\(status)" }
            }
            """.utf8
        )
    }
}

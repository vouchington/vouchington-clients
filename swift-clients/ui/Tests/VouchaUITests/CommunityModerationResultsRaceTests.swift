import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityModerationResultsRaceTests: NativeRouteSurfaceViewModelTestCase {
    func testLateEarlierPostResultCannotOverwriteCurrentModerationSurface() async throws {
        let stalePath = "/api/v1/communities/builders/posts/stale-post/moderation-results"
        let currentPath = "/api/v1/communities/builders/posts/current-post/moderation-results"
        CannedFeedURLProtocol.handlers[stalePath] = (response(status: "rejected"), 200)
        CannedFeedURLProtocol.handlers[currentPath] = (response(status: "approved"), 200)
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
        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["Approved"])

        CannedFeedURLProtocol.releaseResponse(path: stalePath)
        await staleLoad.value

        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["Approved"])
    }

    private func response(status: String) -> Data {
        Data(
            """
            {
              "community_agent_moderations": [],
              "platform_moderation": { "status": "\(status)" }
            }
            """.utf8
        )
    }
}

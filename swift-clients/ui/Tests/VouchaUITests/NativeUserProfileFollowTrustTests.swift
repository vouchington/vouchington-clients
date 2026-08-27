import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeUserProfileFollowTrustTests: NativeRouteSurfaceViewModelTestCase {
    func testSuccessfulFollowReloadsServerHydratedTrustBallot() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/bob"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        viewModel.detailRelationEntityType = "user"
        viewModel.detailRelationEntityId = "user-2"
        viewModel.detailRelationBookmarks = ["follow": false]
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/user-2/follow"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-2/vouch-context"] = (
            Data(
                #"{"positive_by_following":{"total":4,"users":[]},"negative_by_following":{"total":2,"users":[]},"election_vote":{"__entity_type":"election_vote","entity_id":"user-2","user_id":"user-1","choice":"like","created_at":"2026-01-01T00:00:00Z"}}"#
                    .utf8
            ),
            200
        )

        await viewModel.toggleDetailRelation(predicate: "follow")

        XCTAssertTrue(viewModel.isDetailRelationActive("follow"))
        XCTAssertEqual(viewModel.userProfile.trustChoice, .like)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/bookmarks/user/user-2/follow",
            "/api/v1/users/user-2/vouch-context"
        ])
    }
}

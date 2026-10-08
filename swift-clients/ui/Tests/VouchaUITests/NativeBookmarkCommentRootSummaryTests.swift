import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeBookmarkCommentRootSummaryTests: NativeRouteSurfaceViewModelTestCase {
    func testBookmarkedCommentOpensSuppliedRootWithoutRefetchingComment() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(id: "user-1", username: "reader"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/posts/saved"] = (
            Data("""
            {
              "results": [{
                "id": "comment-1", "post_type": "comment", "title": null,
                "root_post_id": "comment-root-post", "created_by_id": "user-2"
              }],
              "page_info": {"has_next_page": false, "start_cursor": null, "end_cursor": null}
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-root-post"] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/posts/saved"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()
        let row = try XCTUnwrap(viewModel.bookmarkRows.first)
        XCTAssertEqual(row.destination, .comment(id: "comment-1", rootId: "comment-root-post"))

        let path = await viewModel.bookmarkDestinationPath(for: row)
        XCTAssertEqual(path, "/discussion/comment-root/comment/comment-1")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/identity",
            "/api/v1/users/user-1/posts/saved",
            "/api/v1/posts/comment-root-post"
        ])
    }
}
